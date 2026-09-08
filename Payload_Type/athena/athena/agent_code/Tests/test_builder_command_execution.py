import importlib
import asyncio
import io
import json
import pathlib
import re
import shutil
import sys
import tempfile
import types
import unittest
import warnings
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path
from types import SimpleNamespace
from unittest import IsolatedAsyncioTestCase, mock

BUILDER_PATH = Path(__file__).parents[2] / "mythic" / "agent_functions" / "builder.py"
AGENT_CODE = Path(__file__).parents[1]
BUILD_STEPS = []


class PayloadType:
    pass


class SupportedOS:
    Windows = "windows"
    Linux = "linux"
    MacOS = "macos"


class BuildStep:
    def __init__(self, **kwargs):
        self.__dict__.update(kwargs)


class BuildParameter:
    def __init__(self, **kwargs):
        self.__dict__.update(kwargs)


class BuildParameterType:
    Boolean = "boolean"
    ChooseOne = "choose-one"
    String = "string"


class BuildStatus:
    Success = "success"
    Error = "error"


class BuildResponse:
    def __init__(self, status):
        self.status = status
        self.payload = None
        self.build_message = None
        self.build_stdout = None
        self.build_stderr = None

    def set_build_stdout(self, value):
        self.build_stdout = value


class MythicRPCPayloadUpdateBuildStepMessage:
    def __init__(self, **kwargs):
        self.__dict__.update(kwargs)


async def record_build_step(message):
    BUILD_STEPS.append(message)
    return SimpleNamespace(Success=True, Error="")


class Logger:
    def info(self, *args, **kwargs):
        pass

    def critical(self, *args, **kwargs):
        pass


def copy_source_tree(source, destination):
    return shutil.copytree(
        source,
        destination,
        dirs_exist_ok=True,
        ignore=shutil.ignore_patterns("bin", "obj", "Tests", "*.user"),
    )


def load_builder_module():
    payload_builder = types.ModuleType("mythic_container.PayloadBuilder")
    exported = (
        PayloadType,
        SupportedOS,
        BuildStep,
        BuildParameter,
        BuildParameterType,
        BuildStatus,
        BuildResponse,
    )
    for value in exported:
        setattr(payload_builder, value.__name__, value)
    payload_builder.__all__ = [value.__name__ for value in exported] + ["pathlib"]
    payload_builder.pathlib = pathlib

    command_base = types.ModuleType("mythic_container.MythicCommandBase")
    command_base.__all__ = []
    rpc = types.ModuleType("mythic_container.MythicRPC")
    rpc.MythicRPCPayloadUpdateBuildStepMessage = MythicRPCPayloadUpdateBuildStepMessage
    rpc.SendMythicRPCPayloadUpdatebuildStep = record_build_step
    rpc.__all__ = [
        "MythicRPCPayloadUpdateBuildStepMessage",
        "SendMythicRPCPayloadUpdatebuildStep",
    ]
    logging = types.ModuleType("mythic_container.logging")
    logging.logger = Logger()
    logging.__all__ = ["logger"]

    package_name = "builder_test_agent_functions"
    agent_functions = types.ModuleType(package_name)
    agent_functions.__path__ = [str(BUILDER_PATH.parent)]
    athena_utils = types.ModuleType(f"{package_name}.athena_utils")
    athena_utils.__path__ = [str(BUILDER_PATH.parent / "athena_utils")]
    athena_utils.plugin_utilities = SimpleNamespace(
        get_unloadable_commands=lambda: [],
        get_nidhogg_commands=lambda: [],
        get_ds_commands=lambda: [],
        get_coff_commands=lambda: [],
        get_inject_shellcode_commands=lambda: [],
    )
    athena_utils.mac_bundler = SimpleNamespace()

    dir_util = types.ModuleType("distutils.dir_util")
    dir_util.copy_tree = copy_source_tree
    distutils = types.ModuleType("distutils")
    sys.modules.update(
        {
            "mythic_container": types.ModuleType("mythic_container"),
            "mythic_container.PayloadBuilder": payload_builder,
            "mythic_container.MythicCommandBase": command_base,
            "mythic_container.MythicRPC": rpc,
            "mythic_container.logging": logging,
            "pefile": SimpleNamespace(PE=object),
            "distutils": distutils,
            "distutils.dir_util": dir_util,
            package_name: agent_functions,
            f"{package_name}.athena_utils": athena_utils,
        }
    )
    spec = importlib.util.spec_from_file_location(f"{package_name}.builder", BUILDER_PATH)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def decode_generated_config(source):
    key = int(re.search(r"private static readonly byte _k = 0x([0-9A-F]{2});", source).group(1), 16)
    encoded = bytes(
        int(value, 16)
        for value in re.findall(
            r"0x([0-9A-F]{2})", source.split("private static readonly byte _k")[0]
        )
    )
    return json.loads(bytes(value ^ key for value in encoded).decode())


builder_module = load_builder_module()


class BuilderCommandExecutionTests(IsolatedAsyncioTestCase):
    async def test_crypto_provider_selection_is_sanitized_and_written_as_msbuild_property(self):
        builder = builder_module.athena()
        builder._project_references = []
        with tempfile.TemporaryDirectory() as workspace:
            project = Path(workspace) / "AthenaCore" / "AthenaCore.csproj"
            project.parent.mkdir()
            project.write_text(
                '<Project Sdk="Microsoft.NET.Sdk">\n'
                '  <PropertyGroup><CryptoProvider>Aes</CryptoProvider></PropertyGroup>\n'
                '</Project>\n'
            )
            build_path = SimpleNamespace(name=workspace)

            await builder.addCrypto(build_path, "None")

            root = ET.parse(project).getroot()
            self.assertEqual("None", root.find(".//CryptoProvider").text)
            self.assertEqual([], builder._project_references)
            with self.assertRaisesRegex(ValueError, "Aes or None"):
                await builder.addCrypto(build_path, "../../Injected")
            self.assertEqual("None", ET.parse(project).findtext(".//CryptoProvider"))

    async def test_build_command_passes_selected_crypto_provider(self):
        builder = builder_module.athena()
        builder.uuid = "crypto-command"
        builder.selected_os = "Linux"
        builder._crypto_provider = "None"
        values = {
            "assemblyname": "Athena",
            "compressed": True,
            "configuration": "Release",
            "invariantglobalization": False,
            "output-type": "binary",
            "self-contained": True,
            "single-file": True,
            "stacktracesupport": True,
            "trimmed": False,
            "usesystemresourcekeys": False,
        }
        builder.get_parameter = values.__getitem__

        command = builder.getBuildCommand("linux-x64")

        self.assertEqual(1, command.count("/p:CryptoProvider=None"))
        self.assertEqual(1, command.count("/p:IncludeNativeLibrariesForSelfExtract=True"))

    async def test_obfuscated_source_rewrite_activates_validated_broad_graph_rename(self):
        builder = builder_module.athena()
        builder.uuid = "37eb846a-12b9-45d5-a49c-8e10754cc0ba"
        builder.selected_os = "RedHat"
        builder._crypto_provider = "None"
        builder.get_parameter = {"configuration": "Debug"}.__getitem__
        commands = []

        async def capture(command, cwd):
            commands.append((command, cwd))
            binary = Path(cwd) / "Obfuscator/bin/Release/net10.0/obfuscator.dll"
            binary.parent.mkdir(parents=True, exist_ok=True)
            binary.write_bytes(b"tool")
            return b"", b""

        builder._run_checked = capture
        with tempfile.TemporaryDirectory() as workspace:
            await builder.rewrite_payload_source(workspace)

        rewrite = next(command for command, _ in commands if "rewrite-source" in command)
        expected = {
            "--broad-semantic-rename": None,
            "--project-root": "AthenaCore/AthenaCore.csproj",
            "--configuration": "Debug",
            "--handler-os": "redhat",
            "--crypto-provider": "None",
        }
        for option, value in expected.items():
            self.assertEqual(1, rewrite.count(option), option)
            if value is not None:
                self.assertEqual(value, rewrite[rewrite.index(option) + 1])
        self.assertIsInstance(rewrite, list)

    async def test_cache_key_changes_with_crypto_provider_without_containing_psk(self):
        builder = builder_module.athena()
        builder.selected_os = "Linux"
        values = {
            "arch": "x64", "compressed": True, "configuration": "Release",
            "invariantglobalization": False, "obfuscate": False,
            "output-type": "binary", "self-contained": True,
            "single-file": True, "stacktracesupport": True, "trimmed": False,
            "usesystemresourcekeys": False,
        }
        builder.get_parameter = values.__getitem__
        builder.commands = SimpleNamespace(get_commands=lambda: [])
        builder._toolchain_identity = lambda: "sdk"
        secret = "controller-test-secret"
        parameters = {"AESPSK": {"enc_key": secret}}
        builder.c2info = [
            SimpleNamespace(
                get_c2profile=lambda: {"name": "http"},
                get_parameters_dict=lambda: parameters,
            )
        ]
        with tempfile.TemporaryDirectory() as source:
            source = Path(source)
            (source / "source.cs").write_text("class Source {}")
            builder.agent_code_path = source
            aes_key = builder._structural_cache_key()
            parameters["AESPSK"] = {"enc_key": ""}
            none_key = builder._structural_cache_key()

        self.assertNotEqual(aes_key, none_key)
        self.assertNotIn(secret, aes_key)

    async def test_gather_files_filters_generated_artifacts_but_keeps_runtime_inputs(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as source, tempfile.TemporaryDirectory() as destination:
            source = Path(source)
            destination = Path(destination)
            required = {
                "AthenaCore/AthenaCore.csproj": "<Project />",
                "AthenaCore/Program.cs": "class Program {}",
                "Directory.Build.props": "<Project />",
                "Tools/AssemblyNameObfuscator/bin/Release/net10.0/AssemblyNameObfuscator.dll": "trusted-tool",
            }
            ignored = {
                "AthenaCore/bin/Release/stale.dll": "stale",
                "AthenaCore/obj/project.assets.json": "stale",
                "AthenaCore/.vs/state.json": "stale",
                "Tests/Agent.Tests/test.cs": "test",
                "nested/TestResults/result.trx": "result",
                "nested/__pycache__/module.pyc": "cache",
                ".pytest_cache/state": "pytest-cache",
                "nested/crash.dmp": "dump",
                "nested/build.binlog": "log",
                "nested/old-output.zip": "archive",
            }
            for relative, content in {**required, **ignored}.items():
                path = source / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content)

            builder._copy_filtered_source(source, destination)

            for relative in required:
                self.assertTrue((destination / relative).is_file(), relative)
            for relative in ignored:
                self.assertFalse((destination / relative).exists(), relative)

    async def test_filtered_source_traversal_is_deterministic(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as source:
            source = Path(source)
            for relative in (
                "b.cs",
                "a.cs",
                "zeta/d.cs",
                "zeta/c.cs",
                "alpha/f.cs",
                "alpha/e.cs",
            ):
                path = source / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(relative)

            def shuffled_walk(root):
                directories = ["zeta", "alpha"]
                yield str(source), directories, ["b.cs", "a.cs"]
                for directory in directories:
                    files = (
                        ["d.cs", "c.cs"]
                        if directory == "zeta"
                        else ["f.cs", "e.cs"]
                    )
                    yield str(source / directory), [], files

            with mock.patch.object(builder_module.os, "walk", side_effect=shuffled_walk):
                observed = [
                    relative.as_posix()
                    for relative, _ in builder._iter_filtered_source_files(source)
                ]

        self.assertEqual(
            ["a.cs", "b.cs", "alpha/e.cs", "alpha/f.cs", "zeta/c.cs", "zeta/d.cs"],
            observed,
        )

    async def test_project_references_are_batched_xml_safe_and_deduplicated(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as workspace:
            project = Path(workspace) / "AthenaCore" / "AthenaCore.csproj"
            project.parent.mkdir()
            project.write_text(
                '<Project Sdk="Microsoft.NET.Sdk">\n'
                '  <!-- preserve this -->\n'
                '  <ItemGroup Condition="\'$(Keep)\' == \'true\'">\n'
                '    <ProjectReference Include="..\\Agent.Crypto.Aes\\Agent.Crypto.Aes.csproj" />\n'
                '  </ItemGroup>\n'
                '</Project>\n'
            )

            builder._write_project_references(
                workspace,
                [
                    "commands/a&b/a&b.csproj",
                    "Agent.Profiles.Http/Agent.Profiles.Http.csproj",
                    "Agent.Crypto.Aes/Agent.Crypto.Aes.csproj",
                    "Agent.Profiles.Http/Agent.Profiles.Http.csproj",
                ],
            )

            source = project.read_text()
            root = ET.fromstring(source)
            includes = [element.attrib["Include"] for element in root.iter("ProjectReference")]
            crypto_group_conditions = [
                group.attrib.get("Condition")
                for group in root.iter("ItemGroup")
                if any(
                    "Agent.Crypto.Aes" in child.attrib.get("Include", "")
                    for child in group.findall("ProjectReference")
                )
            ]
            self.assertIn("<!-- preserve this -->", source)
            self.assertIn("Condition=\"'$(Keep)' == 'true'\"", source)
            self.assertEqual(2, sum("Agent.Crypto.Aes" in value for value in includes))
            self.assertIn(None, crypto_group_conditions)
            self.assertEqual(1, sum("Agent.Profiles.Http" in value for value in includes))
            self.assertIn("../commands/a&b/a&b.csproj", includes)
            self.assertIn("a&amp;b", source)
            self.assertLess(
                source.index("Agent.Profiles.Http"), source.index("commands/a&amp;b")
            )

    async def test_custom_obfuscator_uses_same_uuid_seed_for_source_and_il(self):
        builder = builder_module.athena()
        builder.uuid = "37eb846a-12b9-45d5-a49c-8e10754cc0ba"
        builder.selected_os = "Linux"
        builder._crypto_provider = "Aes"
        builder.get_parameter = {
            "single-file": False,
            "assemblyname": "Athena",
            "configuration": "Release",
        }.__getitem__
        commands = []

        async def capture(command, cwd):
            commands.append((command, cwd))
            binary = Path(cwd) / "Obfuscator/bin/Release/net10.0/obfuscator.dll"
            binary.parent.mkdir(parents=True, exist_ok=True)
            binary.write_bytes(b"tool")
            return b"", b""

        builder._run_checked = capture
        with tempfile.TemporaryDirectory() as workspace:
            await builder.rewrite_payload_source(workspace)
            await builder.obfuscate_published_assemblies(
                SimpleNamespace(name=workspace), str(Path(workspace) / "publish")
            )

        seed = str(builder_module.derive_obfuscation_seed(builder.uuid))
        rewrite = next(command for command, _ in commands if "rewrite-source" in command)
        il_batch = next(command for command, _ in commands if "rewrite-il-batch" in command)
        self.assertEqual(seed, rewrite[rewrite.index("--seed") + 1])
        self.assertEqual(seed, il_batch[il_batch.index("--seed") + 1])
        self.assertEqual(builder.uuid, rewrite[rewrite.index("--uuid") + 1])
        self.assertNotIn("--skip-assembly-rename", il_batch)

    async def test_payload_il_command_has_exact_first_party_assembly_allowlist(self):
        builder = builder_module.athena()
        builder.uuid = "37eb846a-12b9-45d5-a49c-8e10754cc0ba"
        builder.selected_os = "Linux"
        builder.c2info = [SimpleNamespace(get_parameters_dict=lambda: {"url": "secret-c2"})]
        builder.get_parameter = {
            "single-file": False,
            "assemblyname": "Entry.RandomName",
        }.__getitem__
        builder._project_references = [
            "Agent.Profiles.Http/Agent.Profiles.Http.csproj",
            "Agent.Crypto.Aes/Agent.Crypto.Aes.csproj",
            "echo/echo.csproj",
        ]
        commands = []

        async def capture(command, cwd):
            commands.append(command)
            binary = Path(cwd) / "Obfuscator/bin/Release/net10.0/obfuscator.dll"
            binary.parent.mkdir(parents=True, exist_ok=True)
            binary.write_bytes(b"tool")
            return b"", b""

        with tempfile.TemporaryDirectory() as workspace:
            workspace = Path(workspace)
            projects = {
                "Agent.Models/Agent.Models.csproj": "Contracts",
                "Agent.Managers.Linux/Agent.Managers.Linux.csproj": "Linux.Manager",
                "Agent.Managers.Reflection/Agent.Managers.Reflection.csproj": "Reflection.Manager",
                "Agent.Managers.Python/Agent.Managers.Python.csproj": "Python.Manager",
                "Agent.Profiles.Http/Agent.Profiles.Http.csproj": "Http.Profile",
                "Agent.Crypto.Aes/Agent.Crypto.Aes.csproj": "Payload.Crypto",
                "echo/echo.csproj": "Embedded.Echo",
            }
            for relative, identity in projects.items():
                project = workspace / relative
                project.parent.mkdir(parents=True, exist_ok=True)
                project.write_text(
                    f"<Project><PropertyGroup><AssemblyName>{identity}</AssemblyName>"
                    "</PropertyGroup></Project>"
                )
            builder._run_checked = capture
            await builder.obfuscate_published_assemblies(
                SimpleNamespace(name=str(workspace)), str(workspace / "publish")
            )

        il_batch = next(command for command in commands if "rewrite-il-batch" in command)
        allowed = [
            il_batch[index + 1]
            for index, value in enumerate(il_batch)
            if value == "--first-party-assembly"
        ]
        self.assertEqual(
            sorted([
                "Contracts", "Embedded.Echo", "Entry.RandomName", "Http.Profile",
                "Linux.Manager", "Payload.Crypto", "Python.Manager", "Reflection.Manager",
            ], key=str.casefold),
            allowed,
        )
        self.assertNotIn(builder.uuid, allowed)
        self.assertNotIn("secret-c2", allowed)

    async def test_obfuscated_multifile_package_excludes_obfuscation_maps(self):
        BUILD_STEPS.clear()
        builder = builder_module.athena()
        builder.uuid = "37eb846a-12b9-45d5-a49c-8e10754cc0ba"
        builder._build_started = builder_module.time.monotonic()
        builder.get_parameter = {"single-file": False}.__getitem__
        builder._first_party_assembly_names = lambda workspace: ["Athena", "echo"]
        commands = []

        async def capture(command, cwd):
            commands.append(command)
            map_path = Path(command[command.index("--map") + 1])
            map_path.parent.mkdir(parents=True, exist_ok=True)
            map_path.write_text('{"Athena":"renamed"}')
            return b"", b""

        builder._run_checked = capture
        with tempfile.TemporaryDirectory() as workspace:
            workspace = Path(workspace)
            binary = workspace / "Obfuscator/bin/Release/net10.0/obfuscator.dll"
            binary.parent.mkdir(parents=True)
            binary.write_bytes(b"tool")
            output = workspace / "publish"
            output.mkdir()
            (output / "Athena").write_bytes(b"entry")
            (output / "echo.dll").write_bytes(b"plugin")

            await builder.obfuscate_published_assemblies(
                SimpleNamespace(name=str(workspace)), str(output)
            )
            copied_source_map = output / "metadata/source-obf-map.json"
            copied_source_map.parent.mkdir()
            copied_source_map.write_text('{"source":"renamed"}')
            response = await builder._package(
                BuildResponse(BuildStatus.Error),
                SimpleNamespace(name=str(workspace)),
                str(output),
                b"published",
                False,
            )

            il_command = next(
                command for command in commands if "rewrite-il-batch" in command
            )
            map_path = Path(il_command[il_command.index("--map") + 1])
            with zipfile.ZipFile(io.BytesIO(response.payload)) as archive:
                names = archive.namelist()

        self.assertFalse(map_path.is_relative_to(output))
        self.assertEqual(
            ["Athena", "echo.dll"],
            sorted(name for name in names if not name.endswith("/")),
        )
        self.assertFalse(
            any("obf" in name.lower() and "map" in name.lower() for name in names)
        )

    async def test_compile_uses_publish_as_the_only_normal_compile(self):
        BUILD_STEPS.clear()
        builder = builder_module.athena()
        builder.uuid = "compile-test"
        builder.selected_os = "Linux"
        values = {
            "assemblyname": "Athena",
            "compressed": False,
            "configuration": "Release",
            "invariantglobalization": False,
            "obfuscate": False,
            "output-type": "binary",
            "self-contained": False,
            "single-file": False,
            "stacktracesupport": True,
            "trimmed": False,
            "usesystemresourcekeys": False,
        }
        builder.get_parameter = values.__getitem__
        commands = []

        async def run(command, cwd):
            commands.append(command)
            return b"published", b""

        builder._run_checked = run
        with tempfile.TemporaryDirectory() as workspace:
            failure, output_path, stdout = await builder._compile(
                BuildResponse(BuildStatus.Error),
                SimpleNamespace(name=workspace),
                "linux-x64",
                "Athena",
            )

        self.assertIsNone(failure)
        self.assertEqual(b"published", stdout)
        self.assertEqual(1, len(commands))
        self.assertEqual(["dotnet", "publish", "AthenaCore"], commands[0][:3])
        self.assertRegex(BUILD_STEPS[-1].StepStdout, r"Publish elapsed: \d+\.\d{3}s")

    async def test_incremental_cache_serializes_same_key_without_blocking_distinct_keys(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as cache_root:
            builder._cache_root = Path(cache_root)
            same_active = 0
            same_peak = 0
            first_entered = asyncio.Event()
            distinct_entered = {"a": asyncio.Event(), "b": asyncio.Event()}
            release_distinct = asyncio.Event()
            workspaces = []

            async def same_key_worker(name, wait_for_first=False):
                nonlocal same_active, same_peak
                if wait_for_first:
                    await first_entered.wait()
                async with builder._incremental_cache_guard("same-key"):
                    with tempfile.TemporaryDirectory() as workspace:
                        workspace = Path(workspace)
                        workspaces.append(workspace)
                        builder._restore_incremental_cache("same-key", workspace)
                        self.assertFalse((workspace / "generated-secret.txt").exists())
                        same_active += 1
                        same_peak = max(same_peak, same_active)
                        if name == "first":
                            first_entered.set()
                            await asyncio.sleep(0.05)
                            artifact = workspace / "Agent.Models" / "obj" / "incremental.txt"
                            artifact.parent.mkdir(parents=True)
                            artifact.write_text("reused")
                            publish_output = (
                                workspace
                                / "AthenaCore"
                                / "bin"
                                / "Release"
                                / "net10.0"
                                / "linux-x64"
                                / "publish"
                                / "prior-payload"
                            )
                            publish_output.parent.mkdir(parents=True)
                            publish_output.write_text("must-not-be-reused")
                            (workspace / "generated-secret.txt").write_text("do-not-cache")
                            builder._save_incremental_cache("same-key", workspace)
                        else:
                            self.assertEqual(
                                "reused",
                                (workspace / "Agent.Models" / "obj" / "incremental.txt").read_text(),
                            )
                            self.assertFalse(
                                (
                                    workspace
                                    / "AthenaCore"
                                    / "bin"
                                    / "Release"
                                    / "net10.0"
                                    / "linux-x64"
                                    / "publish"
                                    / "prior-payload"
                                ).exists()
                            )
                        same_active -= 1

            async def distinct_worker(key):
                async with builder._incremental_cache_guard(key):
                    distinct_entered[key].set()
                    await release_distinct.wait()

            await asyncio.gather(
                same_key_worker("first"),
                same_key_worker("second", wait_for_first=True),
            )
            distinct_tasks = [
                asyncio.create_task(distinct_worker("a")),
                asyncio.create_task(distinct_worker("b")),
            ]
            await asyncio.wait_for(
                asyncio.gather(*(event.wait() for event in distinct_entered.values())),
                timeout=1,
            )
            release_distinct.set()
            await asyncio.gather(*distinct_tasks)

            self.assertEqual(1, same_peak)
            self.assertEqual(2, len(set(workspaces)))
            self.assertEqual(0o700, builder._cache_root.stat().st_mode & 0o777)

            holder_entered = asyncio.Event()
            release_holder = asyncio.Event()

            async def hold_cancellation_key():
                async with builder._incremental_cache_guard("cancel-key"):
                    holder_entered.set()
                    await release_holder.wait()

            holder = asyncio.create_task(hold_cancellation_key())
            await holder_entered.wait()
            waiter = asyncio.create_task(
                builder._incremental_cache_guard("cancel-key").__aenter__()
            )
            await asyncio.sleep(0.01)
            waiter.cancel()
            with self.assertRaises(asyncio.CancelledError):
                await waiter
            release_holder.set()
            await holder
            async with asyncio.timeout(1):
                async with builder._incremental_cache_guard("cancel-key"):
                    pass

    async def test_build_cancellation_during_cache_lock_preserves_cancellation(self):
        builder = builder_module.athena()
        builder.uuid = "cancel-build"
        builder.selected_os = "Linux"
        builder.c2info = []
        builder.commands = SimpleNamespace(get_commands=lambda: [])
        builder._structural_cache_key = lambda: "cache-key"
        values = {
            "assemblyname": "Athena",
            "obfuscate": False,
            "output-type": "source",
        }
        builder.get_parameter = values.__getitem__

        @builder_module.asynccontextmanager
        async def cancelled_guard(key):
            raise asyncio.CancelledError
            yield key

        builder._incremental_cache_guard = cancelled_guard
        with self.assertRaises(asyncio.CancelledError):
            await builder.build()

    async def test_incremental_cache_prunes_completed_entries_by_total_size(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as cache_root:
            builder._cache_root = Path(cache_root)
            builder._cache_entry_limit = 10
            builder._cache_byte_limit = 10
            builder._ensure_private_cache_root()
            artifacts = builder._cache_root / "artifacts"
            oldest = artifacts / ("a" * 64)
            newest = artifacts / ("b" * 64)
            active_staging = artifacts / (("c" * 64) + ".staging.active")
            abandoned_staging = artifacts / (("d" * 64) + ".staging.abandoned")
            for index, entry in enumerate(
                (oldest, newest, active_staging, abandoned_staging), start=1
            ):
                entry.mkdir()
                (entry / "data").write_bytes(b"12345678")
                builder_module.os.utime(entry, (index, index))
            now = builder_module.time.time()
            builder_module.os.utime(active_staging, (now, now))

            builder._prune_incremental_cache()

            self.assertFalse(oldest.exists())
            self.assertTrue(newest.exists())
            self.assertTrue(active_staging.exists())
            self.assertFalse(abandoned_staging.exists())

    async def test_cache_is_pruned_after_concurrent_key_locks_are_released(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as cache_root:
            builder._cache_root = Path(cache_root)
            builder._cache_entry_limit = 3
            builder._cache_byte_limit = 4
            builder._ensure_private_cache_root()

            keys = []
            shards = set()
            candidate = 0
            while len(keys) < 6:
                key = "concurrent-key-{}".format(candidate)
                shard = builder._cache_token(key)[:3]
                if shard not in shards:
                    keys.append(key)
                    shards.add(shard)
                candidate += 1

            all_entered = asyncio.Event()
            entered = 0
            entered_lock = asyncio.Lock()

            async def populate(key):
                nonlocal entered
                context = builder._incremental_cache_guard(key)
                token = await context.__aenter__()
                entry = builder._cache_root / "artifacts" / token
                entry.mkdir()
                (entry / "data").write_bytes(b"xy")
                async with entered_lock:
                    entered += 1
                    if entered == len(keys):
                        all_entered.set()
                await all_entered.wait()
                await builder._release_incremental_cache_best_effort(context)

            await asyncio.gather(*(populate(key) for key in keys))

            completed = [
                path
                for path in (builder._cache_root / "artifacts").iterdir()
                if builder._CACHE_ENTRY_PATTERN.fullmatch(path.name)
            ]
            self.assertLessEqual(len(completed), builder._cache_entry_limit)
            self.assertLessEqual(
                sum(builder._directory_size(path) for path in completed),
                builder._cache_byte_limit,
            )

    async def test_cache_lock_release_failures_do_not_escape(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as cache_root:
            builder._cache_root = Path(cache_root)
            builder._ensure_private_cache_root()
            real_flock = builder_module.fcntl.flock
            close_calls = []
            unlock_descriptors = []

            def fail_unlock(descriptor, operation):
                if operation == builder_module.fcntl.LOCK_UN:
                    unlock_descriptors.append(descriptor)
                    raise OSError("unlock unavailable")
                return real_flock(descriptor, operation)

            real_close = builder_module.os.close

            def record_close(descriptor):
                close_calls.append(descriptor)
                return real_close(descriptor)

            with mock.patch.object(builder_module.fcntl, "flock", side_effect=fail_unlock), mock.patch.object(
                builder_module.os, "close", side_effect=record_close
            ):
                async with builder._incremental_cache_guard("release-fallback"):
                    pass

            self.assertEqual(1, len(unlock_descriptors))
            self.assertIn(unlock_descriptors[0], close_calls)

    async def test_cache_key_failures_disable_cache_without_failing_build(self):
        builder = builder_module.athena()
        builder.selected_os = "Linux"
        builder.uuid = "cache-key-fallback"
        builder.c2info = []
        builder.commands = SimpleNamespace(get_commands=lambda: [])
        builder.get_parameter = lambda name: "source" if name == "output-type" else False
        builder._validated_assembly_name = lambda: "Athena"
        builder.getRid = lambda: "linux-x64"
        builder._structural_cache_key = lambda: (_ for _ in ()).throw(OSError("unreadable source"))
        observed_cache_keys = []

        async def gather(resp, workspace, cache_key):
            observed_cache_keys.append(cache_key)
            return None

        async def profiles(resp, workspace):
            return None, "", []

        async def succeed(*args):
            return None

        expected = BuildResponse(BuildStatus.Success)

        async def package(*args):
            return expected

        builder._gather_files = gather
        builder._configure_profiles = profiles
        builder._configure_agent = succeed
        builder._add_tasks = succeed
        builder._package = package

        response = await builder.build()

        self.assertIs(expected, response)
        self.assertEqual([None], observed_cache_keys)

    async def test_cache_excludes_payload_specific_compiled_outputs(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as cache_root, tempfile.TemporaryDirectory() as workspace:
            builder._cache_root = Path(cache_root)
            files = {
                "AthenaCore/bin/Release/agent-with-uuid.dll": b"uuid-and-secret",
                "AthenaCore/obj/Release/agent.cache": b"uuid-and-secret",
                "Agent.Profiles.Http/bin/Release/profile.dll": b"c2-secret",
                "Agent.Profiles.Http/obj/Release/profile.cache": b"c2-secret",
                "Agent.Models/bin/Release/models.dll": b"safe-model",
                "Agent.Models/obj/Release/models.cache": b"safe-model",
            }
            for relative, content in files.items():
                path = Path(workspace) / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content)

            builder._save_incremental_cache("safe-artifacts", workspace)
            entry = builder._cache_entry("safe-artifacts")

            self.assertFalse((entry / "AthenaCore").exists())
            self.assertFalse((entry / "Agent.Profiles.Http").exists())
            self.assertTrue((entry / "Agent.Models/bin/Release/models.dll").is_file())
            self.assertNotIn(b"secret", b"".join(path.read_bytes() for path in entry.rglob("*") if path.is_file()))

    async def test_partial_cache_restore_failure_leaves_clean_workspace(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as cache_root, tempfile.TemporaryDirectory() as workspace:
            builder._cache_root = Path(cache_root)
            entry = builder._cache_entry("partial-restore")
            for relative in ("Agent.Models/bin/a.dll", "Agent.Models/obj/b.cache"):
                path = entry / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"cached")
            real_copy2 = builder_module.shutil.copy2
            copy_count = 0

            def fail_second_copy(source, destination, *args, **kwargs):
                nonlocal copy_count
                copy_count += 1
                if copy_count == 2:
                    raise OSError("corrupt cache entry")
                return real_copy2(source, destination, *args, **kwargs)

            with mock.patch.object(builder_module.shutil, "copy2", side_effect=fail_second_copy):
                restored = builder._restore_incremental_cache_best_effort(
                    "partial-restore", workspace
                )

            self.assertFalse(restored)
            self.assertFalse((Path(workspace) / "Agent.Models/bin").exists())
            self.assertFalse((Path(workspace) / "Agent.Models/obj").exists())

    async def test_cache_root_rejects_symlinks(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as parent:
            real_root = Path(parent) / "real"
            real_root.mkdir()
            cache_link = Path(parent) / "cache-link"
            cache_link.symlink_to(real_root, target_is_directory=True)
            builder._cache_root = cache_link

            with self.assertRaises(OSError):
                builder._ensure_private_cache_root()

            builder._cache_root = cache_link / "nested-cache"
            with self.assertRaises(OSError):
                builder._ensure_private_cache_root()

    async def test_cache_restore_rejects_symlinked_entries(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as cache_root, tempfile.TemporaryDirectory() as workspace:
            builder._cache_root = Path(cache_root)
            builder._ensure_private_cache_root()
            external = Path(cache_root) / "external"
            external.mkdir()
            (external / "Agent.Models").mkdir()
            entry = builder._cache_entry("symlink-entry")
            entry.symlink_to(external, target_is_directory=True)

            restored = builder._restore_incremental_cache_best_effort(
                "symlink-entry", workspace
            )

            self.assertFalse(restored)
            self.assertEqual([], list(Path(workspace).iterdir()))

    async def test_pruning_rejects_symlinked_and_foreign_owned_entries(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as cache_root, tempfile.TemporaryDirectory() as external_root:
            builder._cache_root = Path(cache_root)
            builder._cache_entry_limit = 0
            builder._ensure_private_cache_root()
            artifacts = builder._cache_root / "artifacts"

            external = Path(external_root) / "external-entry"
            external.mkdir()
            (external / "keep").write_text("untouched")
            symlink_entry = artifacts / ("e" * 64)
            symlink_entry.symlink_to(external, target_is_directory=True)

            foreign_entry = artifacts / ("f" * 64)
            foreign_entry.mkdir()
            (foreign_entry / "keep").write_text("untouched")
            real_lstat = builder_module.os.lstat

            def foreign_lstat(path, *args, **kwargs):
                metadata = real_lstat(path, *args, **kwargs)
                if Path(path) == foreign_entry:
                    return SimpleNamespace(
                        st_mode=metadata.st_mode,
                        st_uid=builder_module.os.geteuid() + 1,
                        st_mtime=metadata.st_mtime,
                    )
                return metadata

            with mock.patch.object(builder_module.os, "lstat", side_effect=foreign_lstat):
                builder._prune_incremental_cache()

            self.assertTrue(symlink_entry.is_symlink())
            self.assertEqual("untouched", (external / "keep").read_text())
            self.assertTrue(foreign_entry.exists())

    async def test_pruning_rejects_symlinked_and_foreign_owned_lock_files(self):
        builder = builder_module.athena()
        with tempfile.TemporaryDirectory() as cache_root, tempfile.TemporaryDirectory() as external_root:
            builder._cache_root = Path(cache_root)
            builder._ensure_private_cache_root()
            token = "a" * 64
            victim = builder._cache_root / "artifacts" / token
            victim.mkdir()
            external_lock = Path(external_root) / "external.lock"
            external_lock.write_text("untouched")
            lock_path = builder._cache_lock_path(token)
            lock_path.symlink_to(external_lock)

            with self.assertRaises(OSError):
                builder._remove_cache_directory_if_unlocked(victim, token)
            self.assertTrue(victim.exists())
            self.assertEqual("untouched", external_lock.read_text())

            lock_path.unlink()
            lock_path.write_text("")
            real_fstat = builder_module.os.fstat

            def foreign_fstat(descriptor):
                metadata = real_fstat(descriptor)
                return SimpleNamespace(
                    st_mode=metadata.st_mode,
                    st_uid=builder_module.os.geteuid() + 1,
                )

            with mock.patch.object(builder_module.os, "fstat", side_effect=foreign_fstat):
                with self.assertRaises(OSError):
                    builder._remove_cache_directory_if_unlocked(victim, token)
            self.assertTrue(victim.exists())

    async def test_cache_io_failures_fall_back_to_uncached_builds(self):
        BUILD_STEPS.clear()
        builder = builder_module.athena()
        builder.uuid = "cache-fallback"
        builder._build_started = builder_module.time.monotonic()

        def fail_cache(*args):
            raise OSError("cache unavailable")

        builder._restore_incremental_cache = fail_cache
        builder._save_incremental_cache = fail_cache
        with tempfile.TemporaryDirectory() as source, tempfile.TemporaryDirectory() as workspace:
            source_file = Path(source) / "AthenaCore" / "Program.cs"
            source_file.parent.mkdir(parents=True)
            source_file.write_text("class Program {}")
            builder.agent_code_path = Path(source)

            failure = await builder._gather_files(
                BuildResponse(BuildStatus.Error),
                SimpleNamespace(name=workspace),
                "cache-key",
            )
            saved = builder._save_incremental_cache_best_effort("cache-key", workspace)

            self.assertIsNone(failure)
            self.assertFalse(saved)
            self.assertTrue((Path(workspace) / "AthenaCore" / "Program.cs").is_file())
            self.assertTrue(BUILD_STEPS[-1].StepSuccess)

    async def test_cache_key_tracks_source_and_build_structure_without_secrets(self):
        builder = builder_module.athena()
        builder.selected_os = "Linux"
        values = {
            "arch": "x64",
            "assemblyname": "Athena",
            "compressed": False,
            "configuration": "Release",
            "invariantglobalization": False,
            "obfuscate": False,
            "output-type": "binary",
            "self-contained": False,
            "single-file": False,
            "stacktracesupport": True,
            "trimmed": False,
            "usesystemresourcekeys": False,
        }
        builder.get_parameter = values.__getitem__
        builder.commands = SimpleNamespace(get_commands=lambda: ["ls"])
        builder._toolchain_identity = lambda: "sdk-a"
        c2_value = "synthetic-sensitive-value"
        builder.c2info = [
            SimpleNamespace(
                get_c2profile=lambda: {"name": "http"},
                get_parameters_dict=lambda: {"AESPSK": c2_value},
            )
        ]
        with tempfile.TemporaryDirectory() as source:
            source = Path(source)
            (source / "AthenaCore").mkdir()
            code = source / "AthenaCore" / "Program.cs"
            code.write_text("class Program {}")
            builder.agent_code_path = source

            first = builder._structural_cache_key()
            builder.c2info[0].get_parameters_dict = lambda: {"AESPSK": "different-secret"}
            same_structure = builder._structural_cache_key()
            values["assemblyname"] = "Different.Random.Name"
            randomized_name = builder._structural_cache_key()
            builder._toolchain_identity = lambda: "sdk-b"
            changed_toolchain = builder._structural_cache_key()
            builder._toolchain_identity = lambda: "sdk-a"
            code.write_text("class Program { static int V = 2; }")
            changed_source = builder._structural_cache_key()

        self.assertEqual(first, same_structure)
        self.assertEqual(first, randomized_name)
        self.assertNotEqual(first, changed_toolchain)
        self.assertNotEqual(first, changed_source)
        self.assertRegex(first, r"^[0-9a-f]{64}$")
        self.assertNotIn(c2_value, first)

    async def test_failed_phase_reports_phase_and_total_elapsed_time(self):
        BUILD_STEPS.clear()
        builder = builder_module.athena()
        builder.uuid = "timing-failure"
        builder.selected_os = "Windows"
        builder._build_started = builder_module.time.monotonic()
        values = {
            "assemblyname": "Athena",
            "compressed": False,
            "configuration": "Release",
            "invariantglobalization": False,
            "obfuscate": False,
            "output-type": "binary",
            "self-contained": False,
            "single-file": False,
            "stacktracesupport": True,
            "trimmed": False,
            "usesystemresourcekeys": False,
        }
        builder.get_parameter = values.__getitem__

        async def publish(command, cwd):
            return b"published", b""

        builder._run_checked = publish
        with tempfile.TemporaryDirectory() as workspace:
            response, _, _ = await builder._compile(
                BuildResponse(BuildStatus.Error),
                SimpleNamespace(name=workspace),
                "win-x64",
                "Athena",
            )

        self.assertEqual(BuildStatus.Error, response.status)
        self.assertFalse(BUILD_STEPS[-1].StepSuccess)
        self.assertRegex(BUILD_STEPS[-1].StepStdout, r"Publish elapsed: \d+\.\d{3}s")
        self.assertRegex(response.build_message, r"Total build elapsed: \d+\.\d{3}s")

    async def test_source_builder_produces_configured_payload_archive(self):
        BUILD_STEPS.clear()
        builder = builder_module.athena()
        builder.uuid = "37eb846a-12b9-45d5-a49c-8e10754cc0ba"
        builder.selected_os = "Linux"
        clean_root = tempfile.TemporaryDirectory()
        self.addCleanup(clean_root.cleanup)
        clean_agent_code = Path(clean_root.name) / "agent_code"
        builder._copy_filtered_source(AGENT_CODE, clean_agent_code)
        builder.agent_code_path = clean_agent_code
        cache_root = tempfile.TemporaryDirectory()
        self.addCleanup(cache_root.cleanup)
        builder._cache_root = Path(cache_root.name)
        builder.commands = SimpleNamespace(get_commands=lambda: [])
        profile_parameters = {
            "callback_host": "https://example.test",
            "callback_port": "443",
            "get_uri": "index",
            "post_uri": "submit",
            "query_path_name": "q",
            "proxy_host": "",
            "proxy_port": "",
            "proxy_user": "",
            "proxy_pass": "",
            "headers": {"User-Agent": "Athena"},
            "encrypted_exchange_check": "T",
            "AESPSK": {"enc_key": "integration-key"},
            "callback_interval": "15",
            "callback_jitter": 25,
            "killdate": "2030-01-02",
        }
        builder.c2info = [
            SimpleNamespace(
                get_c2profile=lambda: {"name": "http"},
                get_parameters_dict=lambda: profile_parameters,
            )
        ]
        values = {
            "arch": "x64",
            "assemblyname": "Athena.Source",
            "compressed": False,
            "configuration": "Release",
            "invariantglobalization": False,
            "obfuscate": False,
            "output-type": "source",
            "self-contained": False,
            "single-file": False,
            "stacktracesupport": True,
            "trimmed": False,
            "usesystemresourcekeys": False,
        }
        builder.get_parameter = values.__getitem__

        with warnings.catch_warnings():
            warnings.simplefilter("ignore", ResourceWarning)
            response = await builder.build()

        self.assertEqual(BuildStatus.Success, response.status, response.build_stderr)
        self.assertGreater(len(response.payload), 1000)
        with zipfile.ZipFile(io.BytesIO(response.payload)) as archive:
            names = archive.namelist()
            profile = decode_generated_config(
                archive.read("Agent.Profiles.Http/ChannelConfig.cs").decode()
            )
            agent = decode_generated_config(
                archive.read("AthenaCore/Config/AgentConfigData.cs").decode()
            )
            project = archive.read("AthenaCore/AthenaCore.csproj").decode()
            roots = archive.read("AthenaCore/Roots.xml").decode()
        self.assertIn("Agent.Models/Agent.Models.csproj", names)
        unexpected_generated = [
            name
            for name in names
            if not name.endswith("/")
            and ("/bin/" in name or "/obj/" in name)
            and name
            != "Tools/AssemblyNameObfuscator/bin/Release/net10.0/AssemblyNameObfuscator.dll"
        ]
        self.assertEqual([], unexpected_generated)
        self.assertFalse(any(name.startswith("Tests/") for name in names))
        self.assertEqual("https://example.test", profile["callback_host"])
        self.assertTrue(profile["encrypted_exchange_check"])
        self.assertNotIn("AESPSK", profile)
        self.assertEqual(builder.uuid, agent["uuid"])
        self.assertEqual("integration-key", agent["psk"])
        self.assertEqual(15, agent["callback_interval"])
        self.assertFalse(agent["plugin_contract_fingerprint_required"])
        self.assertIn("Agent.Profiles.Http.csproj", project)
        self.assertIn("Agent.Crypto.Aes.csproj", project)
        self.assertIn('<assembly fullname="Agent.Profiles.HTTP"/>', roots)
        self.assertEqual(
            ["Gather Files", "Configure C2 Profiles", "Configure Agent", "Add Tasks", "Zip"],
            [step.StepName for step in BUILD_STEPS],
        )
        self.assertTrue(all(step.StepSuccess for step in BUILD_STEPS))
        self.assertIn("Incremental cache: miss", BUILD_STEPS[0].StepStdout)
        expected_timings = {
            "Gather Files": "Gather Files elapsed",
            "Configure C2 Profiles": "Profile config elapsed",
            "Configure Agent": "Agent config elapsed",
            "Add Tasks": "Project-reference generation/tasks elapsed",
            "Zip": "Packaging elapsed",
        }
        for step in BUILD_STEPS:
            self.assertRegex(
                step.StepStdout,
                re.escape(expected_timings[step.StepName]) + r": \d+\.\d{3}s",
            )
            self.assertNotIn("integration-key", step.StepStdout)
        self.assertRegex(response.build_message, r"Total build elapsed: \d+\.\d{3}s")


if __name__ == "__main__":
    unittest.main()
