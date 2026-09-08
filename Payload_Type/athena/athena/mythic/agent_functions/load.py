from .athena_utils import plugin_utilities, message_utilities
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from .athena_utils.process_utilities import run_checked
from .athena_utils.argument_utilities import load_json_or_get_shorthand
from .athena_utils.assembly_utilities import effective_assembly_name
import asyncio
import json
import base64
import os
import hashlib
import shutil
import tempfile
import uuid as uuid_module
from contextlib import asynccontextmanager
from pathlib import Path


CONTRACT_FINGERPRINT_DOMAIN = "athena-contract-v1"
CONTRACT_FINGERPRINT_METADATA_KEY = "AthenaPluginContract"


def derive_contract_fingerprint(payload_uuid):
    normalized = str(uuid_module.UUID(str(payload_uuid)))
    material = f"{CONTRACT_FINGERPRINT_DOMAIN}:{normalized}".encode("utf-8")
    return hashlib.sha256(material).hexdigest()


def write_contract_metadata_source(plugin_directory, payload_uuid):
    fingerprint = derive_contract_fingerprint(payload_uuid)
    output = Path(plugin_directory) / "AthenaContractMetadata.g.cs"
    output.write_text(
        "using System.Reflection;\n"
        f'[assembly: AssemblyMetadata("{CONTRACT_FINGERPRINT_METADATA_KEY}", '
        f'"{fingerprint}")]\n',
        encoding="utf-8",
    )
    return output


def registered_command_names():
    """Return commands registered by Mythic in this payload container."""
    pending = list(CommandBase.__subclasses__())
    command_classes = []
    while pending:
        command_class = pending.pop()
        command_classes.append(command_class)
        pending.extend(command_class.__subclasses__())
    return {
        command_class.cmd
        for command_class in command_classes
        if isinstance(getattr(command_class, "cmd", None), str)
    }


def contained_command_directory(agent_code_path, command, registered_commands, suffix=""):
    if not isinstance(command, str) or command not in registered_commands:
        raise ValueError("load requires a registered command")
    root = Path(agent_code_path).resolve()
    candidate = (root / (command + suffix)).resolve()
    try:
        candidate.relative_to(root)
    except ValueError as error:
        raise ValueError("load command directory is not contained in agent_code") from error
    return candidate


_callback_mutation_locks = {}
ROLLBACK_ATTEMPTS = 3
ROLLBACK_RPC_TIMEOUT = 2.0


@asynccontextmanager
async def _callback_mutation_lock(callback_id):
    """Hold one shared callback lock and evict it after its final user."""
    entry = _callback_mutation_locks.get(callback_id)
    if entry is None:
        entry = {"lock": asyncio.Lock(), "references": 0}
        _callback_mutation_locks[callback_id] = entry
    entry["references"] += 1

    acquired = False
    try:
        await entry["lock"].acquire()
        acquired = True
        yield
    finally:
        if acquired:
            entry["lock"].release()
        entry["references"] -= 1
        if (
            entry["references"] == 0
            and _callback_mutation_locks.get(callback_id) is entry
        ):
            del _callback_mutation_locks[callback_id]


async def _await_cancellation_safe(awaitable):
    """Finish a bounded cleanup awaitable despite repeated caller cancellation."""
    task = asyncio.create_task(awaitable)
    cancellation_received = False
    while not task.done():
        try:
            await asyncio.shield(task)
        except asyncio.CancelledError:
            cancellation_received = True
    if cancellation_received:
        if task.cancelled():
            raise asyncio.CancelledError
        task_error = task.exception()
        if task_error is not None:
            raise task_error
        raise asyncio.CancelledError
    return task.result()


async def _remove_callback_commands(task_id, commands):
    """Retry rollback RPCs, with a timeout bounding every attempt."""
    last_error = None
    for _attempt in range(ROLLBACK_ATTEMPTS):
        try:
            response = await asyncio.wait_for(
                SendMythicRPCCallbackRemoveCommand(
                    MythicRPCCallbackRemoveCommandMessage(
                        TaskID=task_id, Commands=commands
                    )
                ),
                ROLLBACK_RPC_TIMEOUT,
            )
            if response.Success:
                return
            last_error = Exception(response.Error)
        except asyncio.TimeoutError:
            last_error = Exception(
                f"rollback RPC timed out after {ROLLBACK_RPC_TIMEOUT} seconds"
            )
        except asyncio.CancelledError:
            rollback_task = asyncio.current_task()
            if rollback_task is not None and rollback_task.cancelling():
                raise
            last_error = Exception("rollback RPC was cancelled")
        except Exception as error:
            last_error = error
    assert last_error is not None
    raise last_error


async def _reconcile_callback_commands(task_id, commands):
    """Remove baseline-absent commands after an add with uncertain effects."""
    try:
        # Re-read for RPC reconciliation/diagnostics, but remove every requested
        # name: the pre-add snapshot and callback lock prove none was pre-owned,
        # while the search result itself may lag an uncertain add side effect.
        await asyncio.wait_for(
            SendMythicRPCCallbackSearchCommand(
                MythicRPCCallbackSearchCommandMessage(TaskID=task_id)
            ),
            ROLLBACK_RPC_TIMEOUT,
        )
    except asyncio.CancelledError:
        reconciliation_task = asyncio.current_task()
        if reconciliation_task is not None and reconciliation_task.cancelling():
            raise
    except Exception:
        pass
    if commands:
        await _remove_callback_commands(task_id, commands)


async def _rollback_load_failure(
    task_id, commands, failure, failure_context, *, reconcile=False
):
    """Roll back this operation's additions without replacing cancellation."""
    if not commands:
        return
    rollback = (
        _reconcile_callback_commands(task_id, commands)
        if reconcile
        else _remove_callback_commands(task_id, commands)
    )
    try:
        await _await_cancellation_safe(rollback)
    except asyncio.CancelledError:
        if isinstance(failure, asyncio.CancelledError):
            return
        raise
    except Exception as rollback_error:
        if isinstance(failure, asyncio.CancelledError):
            failure.add_note(
                f"final rollback failure after {ROLLBACK_ATTEMPTS} attempts: "
                f"{rollback_error}"
            )
            return
        raise Exception(
            f"{failure_context}: {failure}; "
            f"failed to roll back callback commands: {rollback_error}"
        ) from failure


async def prepare_load_dependencies(
    task_id, callback_commands, libraries, callback_id=None
):
    """Atomically prepare dependencies without changing prior callback state."""
    lock_key = task_id if callback_id is None else callback_id
    async with _callback_mutation_lock(lock_key):
        commands_to_add = []
        if callback_commands:
            search = await SendMythicRPCCallbackSearchCommand(
                MythicRPCCallbackSearchCommandMessage(TaskID=task_id)
            )
            if not search.Success:
                raise Exception("Failed to inspect callback commands: " + search.Error)
            existing_commands = {command.Name for command in search.Commands}
            commands_to_add = [
                command for command in callback_commands
                if command not in existing_commands
            ]

        if commands_to_add:
            try:
                response = await SendMythicRPCCallbackAddCommand(
                    MythicRPCCallbackAddCommandMessage(
                        TaskID=task_id, Commands=commands_to_add
                    )
                )
            except asyncio.CancelledError as cancellation:
                await _rollback_load_failure(
                    task_id,
                    commands_to_add,
                    cancellation,
                    "Failed to add commands to callback",
                    reconcile=True,
                )
                raise
            except Exception as error:
                await _rollback_load_failure(
                    task_id,
                    commands_to_add,
                    error,
                    "Failed to add commands to callback",
                    reconcile=True,
                )
                raise
            if not response.Success:
                error = Exception(response.Error)
                await _rollback_load_failure(
                    task_id,
                    commands_to_add,
                    error,
                    "Failed to add commands to callback",
                    reconcile=True,
                )
                raise Exception("Failed to add commands to callback: " + response.Error)

        if not libraries:
            return

        try:
            response = await SendMythicRPCTaskCreateSubtaskGroup(
                MythicRPCTaskCreateSubtaskGroupMessage(
                    TaskID=task_id,
                    GroupName="load-command-dependencies",
                    Tasks=[
                        MythicRPCTaskCreateSubtaskGroupTasks(
                            CommandName="load-assembly",
                            Params=json.dumps(library),
                            ParameterGroupName="InternalLib",
                        )
                        for library in libraries
                    ],
                )
            )
        except asyncio.CancelledError as cancellation:
            await _rollback_load_failure(
                task_id,
                commands_to_add,
                cancellation,
                "Failed to create dependency subtasks",
            )
            raise
        except Exception as error:
            await _rollback_load_failure(
                task_id,
                commands_to_add,
                error,
                "Failed to create dependency subtasks",
            )
            raise
        if not response.Success:
            error = Exception(response.Error)
            await _rollback_load_failure(
                task_id,
                commands_to_add,
                error,
                "Failed to create dependency subtasks",
            )
            raise Exception("Failed to create dependency subtasks: " + response.Error)


COMMAND_LIBRARIES = {
    "ds": [
        {"libraryname": "System.DirectoryServices.Protocols.dll", "target": "plugin"}
    ],
    "ssh": [
        {"libraryname": "Renci.SshNet.dll", "target": "plugin"},
        {"libraryname": "BouncyCastle.Cryptography.dll", "target": "plugin"},
    ],
    "sftp": [
        {"libraryname": "Renci.SshNet.dll", "target": "plugin"},
        {"libraryname": "BouncyCastle.Cryptography.dll", "target": "plugin"},
    ],
    "screenshot": [
        {"libraryname": "System.Drawing.Common.dll", "target": "plugin"}
    ],
}


def _command_families():
    """Fetch each loadable command family once, preserving rejection order."""
    return {
        "coff": plugin_utilities.get_coff_commands(),
        "inject-shellcode": plugin_utilities.get_inject_shellcode_commands(),
        "ds": plugin_utilities.get_ds_commands(),
        "nidhogg": plugin_utilities.get_nidhogg_commands(),
    }


async def _reject_family_command(command, command_families, task):
    for family, family_commands in command_families.items():
        if command in family_commands:
            error = f"Please load {family} to enable this command"
            await message_utilities.send_agent_message(error, task)
            raise Exception(error)


def _plugin_directories(agent_code_path, command, payload_os):
    registered_commands = registered_command_names()
    generic_directory = contained_command_directory(
        agent_code_path, command, registered_commands
    )
    platform_directory = contained_command_directory(
        agent_code_path,
        command,
        registered_commands,
        f"-{payload_os.lower()}",
    )
    return generic_directory, platform_directory


def _select_plugin_directory(generic_directory, platform_directory):
    if platform_directory.is_dir():
        return platform_directory
    if generic_directory.is_dir():
        return generic_directory
    raise Exception(
        f"Failed to compile plugin (Folder: {generic_directory} doesn't exist)"
    )


def _select_plugin_dll(plugin_directory, command, payload_os):
    output_directory = plugin_directory / "bin" / "Release" / "net10.0"
    platform_dll = output_directory / f"{command.lower()}-{payload_os.lower()}.dll"
    generic_dll = output_directory / f"{command.lower()}.dll"
    if platform_dll.is_file():
        return platform_dll
    if generic_dll.is_file():
        return generic_dll
    raise Exception("Failed to compile plugin, plugin not located at: " + str(generic_dll))


def _dependency_metadata(command, command_families):
    return command_families.get(command, []), COMMAND_LIBRARIES.get(command, [])


def _set_asm_argument(task_args, contents, parameter_group):
    encoded_contents = base64.b64encode(contents).decode("utf-8")
    task_args.add_arg(
        "asm",
        encoded_contents,
        parameter_group_info=[
            ParameterGroupInfo(required=True, group_name=parameter_group)
        ],
    )


async def _handle_custom_file(task_data):
    file_response = await SendMythicRPCFileGetContent(
        MythicRPCFileGetContentMessage(task_data.args.get_arg("commandFile"))
    )
    if not file_response.Success:
        error = "Failed to get file contents: " + file_response.Error
        await message_utilities.send_agent_message(error, task_data.Task)
        raise Exception(error)
    _set_asm_argument(task_data.args, file_response.Content, "Custom")


class LoadArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="command", cli_name="command", display_name="Command to Load", type=ParameterType.ChooseOne,
                choices_are_all_commands=True,
                description="Load Command",
                parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    group_name="Default"
                ),
                ParameterGroupInfo(
                    required=True,
                    group_name="Custom"
                )
                ]
            ),
            CommandParameter(
                name="commandFile",
                type=ParameterType.File,
                description="List of hosts in a newline separated file",
                parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    group_name="Custom"
                )]
            )
        ]

    async def parse_arguments(self):
        command_line = load_json_or_get_shorthand(
            self, "load", "Missing command to load"
        )
        if command_line is not None:
            self.add_arg("command", command_line)
        command = self.get_arg("command")
        if not isinstance(command, str) or not command.strip():
            raise ValueError("load requires a nonempty command")
        self.add_arg("command", command.strip())



class LoadCommand(CommandBase):
    cmd = "load"
    needs_admin = False
    help_cmd = "load cmd"
    description = "This loads a new plugin into memory via the C2 channel."
    version = 1
    author = "@checkymander"
    parameters = []
    attackmapping = ["T1129", "T1059.002", "T1620"]
    argument_class = LoadArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def _compile_plugin(
        self, task_data, command, generic_directory, platform_directory
    ):
        plugin_directory = _select_plugin_directory(
            generic_directory, platform_directory
        )
        obfuscate = any(
            parameter.Value
            for parameter in task_data.BuildParameters
            if parameter.Name == "obfuscate"
        )
        single_file = any(
            parameter.Value
            for parameter in task_data.BuildParameters
            if parameter.Name == "single-file"
        )
        compiled = await self.compile_command(
            str(plugin_directory), task_data.Payload.UUID, obfuscate, single_file
        )
        if isinstance(compiled, bytes):
            return compiled
        plugin_dll = _select_plugin_dll(
            plugin_directory, command, task_data.Payload.OS
        )
        with open(plugin_dll, "rb") as plugin_file:
            return plugin_file.read()

    async def create_go_tasking(self, taskData: MythicCommandBase.PTTaskMessageAllData) -> MythicCommandBase.PTTaskCreateTaskingMessageResponse:
        response = MythicCommandBase.PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
        )
        if taskData.args.get_parameter_group_name() == "Custom":
            await _handle_custom_file(taskData)
            return response

        command = taskData.args.get_arg("command")
        generic_directory, platform_directory = _plugin_directories(
            self.agent_code_path, command, taskData.Payload.OS
        )
        command_families = _command_families()
        await _reject_family_command(command, command_families, taskData.Task)

        plugin_contents = await self._compile_plugin(
            taskData, command, generic_directory, platform_directory
        )
        callback_commands, libraries = _dependency_metadata(
            command, command_families
        )
        await prepare_load_dependencies(
            taskData.Task.ID,
            callback_commands,
            libraries,
            taskData.Task.CallbackID,
        )
        _set_asm_argument(taskData.args, plugin_contents, "Default")
        return response
    
    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass

    async def get_commands(self, response: AgentResponse):
        pass

    async def compile_command(
        self, plugin_folder_path, uuid, obfuscate, single_file=True
    ):
        if obfuscate:
            return await self._compile_obfuscated_command(
                plugin_folder_path, uuid, single_file
            )
        command = [
            "dotnet", "build", "-c", "Release",
            "/p:PayloadUUID={}".format(uuid),
            "/p:Obfuscate=False",
        ]
        return await run_checked(command, plugin_folder_path)

    async def _compile_obfuscated_command(
        self, plugin_folder_path, uuid, single_file=True
    ):
        seed = int(hashlib.sha256(uuid.encode()).hexdigest(), 16) & 0x7FFFFFFF
        agent_code = Path(self.agent_code_path)
        obfuscator = (
            agent_code / "Obfuscator/bin/Release/net10.0/obfuscator.dll"
        )
        if not obfuscator.is_file():
            await run_checked(
                [
                    "dotnet", "build",
                    str(agent_code / "Obfuscator/Obfuscator.csproj"),
                    "-c", "Release", "--nologo",
                ],
                str(agent_code),
            )
        if not obfuscator.is_file():
            raise FileNotFoundError(
                "Custom obfuscator build produced no binary: " + str(obfuscator)
            )

        with tempfile.TemporaryDirectory(prefix="athena-plugin-obf-") as temp:
            temp_root = Path(temp)
            plugin_name = Path(plugin_folder_path).name
            plugin_temp = temp_root / plugin_name
            shutil.copytree(
                plugin_folder_path, plugin_temp,
                ignore=shutil.ignore_patterns("bin", "obj"),
            )
            shutil.copytree(
                agent_code / "Agent.Models", temp_root / "Agent.Models",
                ignore=shutil.ignore_patterns("bin", "obj"),
            )
            write_contract_metadata_source(plugin_temp, uuid)

            await run_checked(
                [
                    "dotnet", str(obfuscator), "rewrite-source",
                    "--seed", str(seed), "--uuid", uuid,
                    "--input", str(temp_root),
                    "--output", str(temp_root),
                ],
                str(temp_root),
            )

            project = plugin_temp / (plugin_name + ".csproj")
            if not project.is_file():
                projects = sorted(plugin_temp.glob("*.csproj"))
                if len(projects) != 1:
                    raise FileNotFoundError(
                        "Unable to identify plugin project in " + str(plugin_temp)
                    )
                project = projects[0]
            plugin_identity = effective_assembly_name(project)
            models_identity = effective_assembly_name(
                temp_root / "Agent.Models/Agent.Models.csproj"
            )
            await run_checked(
                [
                    "dotnet", "build", str(project), "-c", "Release",
                    "/p:PayloadUUID={}".format(uuid),
                    "/p:Obfuscate=False",
                ],
                str(plugin_temp),
            )

            build_out = plugin_temp / "bin/Release/net10.0"
            il_command = [
                "dotnet", str(obfuscator), "rewrite-il-batch",
                "--seed", str(seed), "--dir", str(build_out),
                "--map", str(build_out / "obf-map.json"),
                "--skip-file-rename",
            ]
            for assembly_name in sorted(
                {models_identity, plugin_identity}, key=str.casefold
            ):
                il_command.extend(["--first-party-assembly", assembly_name])
            if single_file:
                il_command.append("--skip-assembly-rename")
            await run_checked(
                il_command,
                str(plugin_temp),
            )

            expected = build_out / (plugin_identity + ".dll")
            if not expected.is_file():
                candidates = sorted(
                    path for path in build_out.glob("*.dll")
                    if path.name != "Agent.Models.dll"
                )
                if len(candidates) != 1:
                    raise FileNotFoundError(
                        "Failed to locate obfuscated plugin in " + str(build_out)
                    )
                expected = candidates[0]
            return expected.read_bytes()

