from .athena_utils import plugin_utilities, message_utilities, plugin_registry
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json
import base64
import os
import asyncio
import hashlib
import tempfile
import shutil

class LoadArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="command", cli_name="command",
                display_name="Command to Load",
                type=ParameterType.ChooseOne,
                dynamic_query_function=self.get_unloaded_commands,
                description="Load Command",
                parameter_group_info=[
                    ParameterGroupInfo(
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

    async def get_unloaded_commands(
        self, inputMsg: PTRPCDynamicQueryFunctionMessage
    ) -> PTRPCDynamicQueryFunctionMessageResponse:
        all_cmds = await SendMythicRPCCommandSearch(
            MythicRPCCommandSearchMessage(
                SearchPayloadTypeName="athena"
            )
        )
        loaded_cmds = await SendMythicRPCCallbackSearchCommand(
            MythicRPCCallbackSearchCommandMessage(
                CallbackID=inputMsg.Callback
            )
        )
        loaded_names = set()
        if loaded_cmds.Success:
            loaded_names = {c.Name for c in loaded_cmds.Commands}
        choices = sorted(
            c.Name for c in all_cmds.Commands
            if c.Name not in loaded_names
        )
        return PTRPCDynamicQueryFunctionMessageResponse(
            Success=True, Choices=choices
        )

    async def parse_arguments(self):
        if self.command_line[0] == "{":
                tmpjson = json.loads(self.command_line)
                self.load_args_from_json_string(json.dumps(tmpjson))
        else:
                self.load_args_from_json_string(self.command_line)



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

    async def create_go_tasking(
        self,
        taskData: MythicCommandBase.PTTaskMessageAllData
    ) -> MythicCommandBase.PTTaskCreateTaskingMessageResponse:
        response = MythicCommandBase.PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
        )
        groupName = taskData.args.get_parameter_group_name()
        if groupName == "Custom":
            file = await SendMythicRPCFileGetContent(
                MythicRPCFileGetContentMessage(
                    taskData.args.get_arg("commandFile")
                )
            )

            if file.Success:
                file_contents = base64.b64encode(file.Content)
                taskData.args.add_arg(
                    "asm",
                    file_contents.decode("utf-8"),
                    parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        group_name="Custom"
                    )]
                )
            else:
                await message_utilities.send_agent_message(
                    "Failed to get file contents: " + file.Error,
                    taskData.Task
                )
                raise Exception(
                    "Failed to get file contents: " + file.Error
                )

            return response

        command = taskData.args.get_arg('command')

        parent = plugin_registry.get_parent(command)
        if parent:
            command = parent
            taskData.args.set_arg("command", command)

        loaded_cmds = await SendMythicRPCCallbackSearchCommand(
            MythicRPCCallbackSearchCommandMessage(
                CallbackID=taskData.Callback.ID
            )
        )
        if loaded_cmds.Success:
            loaded_names = {c.Name for c in loaded_cmds.Commands}
            if command in loaded_names:
                raise Exception(
                    f"Module '{command}' is already loaded"
                )

        command_libraries = plugin_registry.get_libraries(command)
        subcommands = plugin_registry.get_subcommands(command)

        plugin_dir_path_platform_specific = os.path.join(
            self.agent_code_path,
            f"{command.lower()}-{taskData.Payload.OS.lower()}"
        )
        plugin_dir_path_generic = os.path.join(
            self.agent_code_path, command
        )

        if not os.path.isdir(plugin_dir_path_platform_specific):
            if not os.path.isdir(plugin_dir_path_generic):
                raise Exception(
                    f"Failed to compile plugin "
                    f"(Folder: {plugin_dir_path_generic} doesn't exist)"
                )
            else:
                valid_path = plugin_dir_path_generic
        else:
            valid_path = plugin_dir_path_platform_specific

        obfuscate = any(
            bp.Value for bp in taskData.BuildParameters
            if bp.Name == "obfuscate"
        )

        dll_path = await self.compile_command(
            valid_path, command, taskData.Payload.UUID,
            taskData.Payload.OS.lower(), obfuscate
        )

        with open(dll_path, 'rb') as file:
            dllBytes = file.read()

        encodedBytes = base64.b64encode(dllBytes)

        if command_libraries:
            for lib in command_libraries:
                print("Kicking off load-assembly for " + json.dumps(lib))
                createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage(
                    taskData.Task.ID,
                    CommandName="load-assembly",
                    Params=json.dumps(lib),
                    ParameterGroupName="InternalLib"
                )
                subtask = await SendMythicRPCTaskCreateSubtask(
                    createSubtaskMessage
                )

        if subcommands:
            resp = await SendMythicRPCCallbackAddCommand(
                MythicRPCCallbackAddCommandMessage(
                    TaskID=taskData.Task.ID,
                    Commands=subcommands
                )
            )
            if not resp.Success:
                raise Exception(
                    "Failed to add commands to callback: " + resp.Error
                )

        sub_list = ", ".join(subcommands) if subcommands else "none"
        await message_utilities.send_agent_message(
            f"Tasked agent to load {command} (provides: {sub_list})\n\n",
            taskData.Task
        )

        taskData.args.add_arg(
            "asm",
            encodedBytes.decode(),
            parameter_group_info=[ParameterGroupInfo(
                required=True,
                group_name="Default"
            )]
        )

        return response

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass

    async def get_commands(self, response: AgentResponse):
        pass

    async def compile_command(
        self, plugin_folder_path, command, uuid,
        target_os, obfuscate
    ):
        temp_dir = tempfile.mkdtemp()
        try:
            plugin_temp = os.path.join(temp_dir, "plugin")
            shutil.copytree(
                plugin_folder_path, plugin_temp,
                ignore=shutil.ignore_patterns("bin", "obj")
            )

            models_src = os.path.join(
                str(self.agent_code_path), "Workflow.Models"
            )
            models_dst = os.path.join(temp_dir, "Workflow.Models")
            shutil.copytree(
                models_src, models_dst,
                ignore=shutil.ignore_patterns("bin", "obj")
            )

            providers_win_src = os.path.join(
                str(self.agent_code_path),
                "Workflow.Providers.Windows"
            )
            if os.path.isdir(providers_win_src):
                providers_win_dst = os.path.join(
                    temp_dir, "Workflow.Providers.Windows"
                )
                shutil.copytree(
                    providers_win_src, providers_win_dst,
                    ignore=shutil.ignore_patterns("bin", "obj")
                )

            obf_seed = None
            if obfuscate:
                obf_seed = int(
                    hashlib.sha256(uuid.encode()).hexdigest(), 16
                ) & 0x7FFFFFFF
                obfuscator_bin = os.path.join(
                    str(self.agent_code_path),
                    "Obfuscator", "bin", "Release",
                    "net10.0", "obfuscator"
                )
                rewrite_proc = await asyncio.create_subprocess_exec(
                    obfuscator_bin, "rewrite-source",
                    "--seed", str(obf_seed),
                    "--uuid", uuid,
                    "--input", temp_dir,
                    "--output", temp_dir,
                    stdout=asyncio.subprocess.PIPE,
                    stderr=asyncio.subprocess.PIPE
                )
                _, r_stderr = await rewrite_proc.communicate()
                if rewrite_proc.returncode != 0:
                    raise Exception(
                        "Source rewrite failed: "
                        + r_stderr.decode()
                    )

            build_proc = await asyncio.create_subprocess_exec(
                "dotnet", "build", "-c", "Release",
                "/p:PayloadUUID=" + uuid,
                cwd=plugin_temp,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            b_stdout, b_stderr = await build_proc.communicate()
            if build_proc.returncode != 0:
                output = b_stdout.decode() + b_stderr.decode()
                raise Exception(
                    "Error compiling plugin: " + output
                )

            dll_name_platform = (
                f"{command.lower()}-{target_os}.dll"
            )
            dll_name_generic = f"{command.lower()}.dll"

            build_out = os.path.join(
                plugin_temp, "bin", "Release", "net10.0"
            )
            dll_platform = os.path.join(
                build_out, dll_name_platform
            )
            dll_generic = os.path.join(
                build_out, dll_name_generic
            )

            if os.path.isfile(dll_platform):
                dll_path = dll_platform
            elif os.path.isfile(dll_generic):
                dll_path = dll_generic
            else:
                raise Exception(
                    "Failed to compile plugin, DLL not found: "
                    + dll_generic
                )

            if obfuscate:
                il_proc = await asyncio.create_subprocess_exec(
                    obfuscator_bin, "rewrite-il-batch",
                    "--seed", str(obf_seed),
                    "--dir", build_out,
                    "--map", os.path.join(build_out, "obf-map.json"),
                    "--skip-assembly-rename",
                    stdout=asyncio.subprocess.PIPE,
                    stderr=asyncio.subprocess.PIPE
                )
                _, il_stderr = await il_proc.communicate()
                if il_proc.returncode != 0:
                    raise Exception(
                        "IL rewrite failed: " + il_stderr.decode()
                    )
                map_path = os.path.join(build_out, "obf-map.json")
                if os.path.isfile(map_path):
                    with open(map_path, "r") as mf:
                        obf_map = json.load(mf)
                    renames = obf_map.get("metadataRenames", {})
                    orig_name = command.lower()
                    new_name = renames.get("asm:" + orig_name, orig_name)
                    dll_path = os.path.join(build_out, new_name + ".dll")
                    if not os.path.isfile(dll_path):
                        orig_plat = f"{command.lower()}-{target_os}"
                        new_plat = renames.get("asm:" + orig_plat, orig_plat)
                        dll_path = os.path.join(build_out, new_plat + ".dll")

            final_dll = os.path.join(
                temp_dir, os.path.basename(dll_path)
            )
            shutil.copy2(dll_path, final_dll)
            return final_dll
        except Exception:
            shutil.rmtree(temp_dir, ignore_errors=True)
            raise
