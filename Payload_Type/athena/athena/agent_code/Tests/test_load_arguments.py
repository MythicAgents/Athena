import asyncio
import os
import sys
import types
import unittest
import tempfile
from unittest import mock
from pathlib import Path

try:
    from mythic_test_bootstrap import load_command
except ModuleNotFoundError:
    from .mythic_test_bootstrap import load_command

PACKAGE = "athena_test_load_agent_functions"
command_directory = Path(__file__).parents[2] / "mythic" / "agent_functions"
utils = types.ModuleType(PACKAGE + ".athena_utils")
utils.__path__ = [str(command_directory / "athena_utils")]
utils.plugin_utilities = types.SimpleNamespace()
utils.message_utilities = types.SimpleNamespace()
process_utilities = types.ModuleType(PACKAGE + ".athena_utils.process_utilities")


async def run_checked(*args, **kwargs):
    return "", ""


process_utilities.run_checked = run_checked
sys.modules[PACKAGE + ".athena_utils"] = utils
sys.modules[PACKAGE + ".athena_utils.process_utilities"] = process_utilities
load_module = load_command("load.py", package_name=PACKAGE)


class LoadArgumentTests(unittest.TestCase):
    def test_load_parses_working_command(self):
        arguments = load_module.LoadArguments("  screenshot  ")
        asyncio.run(arguments.parse_arguments())
        self.assertEqual("screenshot", arguments.get_arg("command"))
        self.assertEqual('{"command": "screenshot"}', arguments.serialize())

    @staticmethod
    def _write_project(root, name, contents="<Project />"):
        project_dir = root / name
        project_dir.mkdir(parents=True, exist_ok=True)
        (project_dir / f"{name}.csproj").write_text(contents)
        return project_dir

    @classmethod
    def _init_obfuscated_workspace(cls, root, plugin_project="<Project />"):
        cls._write_project(root, "Agent.Models")
        cls._write_project(root, "plugin", plugin_project)
        binary = root / "Obfuscator/bin/Release/net10.0/obfuscator.dll"
        binary.parent.mkdir(parents=True, exist_ok=True)
        binary.write_bytes(b"tool")

    @staticmethod
    def _write_built_plugin(cwd):
        output = Path(cwd) / "bin/Release/net10.0"
        output.mkdir(parents=True, exist_ok=True)
        (output / "plugin.dll").write_bytes(b"plugin")

    @staticmethod
    def _first_party_assemblies(commands):
        il_batch = next(item for item in commands if "rewrite-il-batch" in item)
        return [
            il_batch[index + 1]
            for index, value in enumerate(il_batch)
            if value == "--first-party-assembly"
        ]

    def _compile_in_workspace(self, agent_code_path, capture, single_file=True):
        plugin = Path(agent_code_path).resolve() / "plugin"
        command = load_module.LoadCommand()
        command.agent_code_path = agent_code_path
        with mock.patch.object(load_module, "run_checked", capture):
            return asyncio.run(command.compile_command(
                str(plugin),
                "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
                True,
                single_file,
            ))

    def _compile_obfuscated_plugin(
        self, single_file, plugin_project="<Project />", models_project="<Project />"
    ):
        commands = []

        async def capture(command, cwd):
            commands.append(command)
            if "build" in command and str(command[2]).endswith("plugin.csproj"):
                self._write_built_plugin(cwd)
            return "", ""

        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            self._init_obfuscated_workspace(root, plugin_project)
            self._write_project(root, "Agent.Models", models_project)
            payload = self._compile_in_workspace(root, capture, single_file)

        return payload, commands

    def test_obfuscator_fallback_build_resolves_relative_agent_code_path(self):
        commands = []

        async def capture(command, cwd):
            commands.append(command)
            if "build" in command and str(command[2]).endswith("Obfuscator.csproj"):
                output = Path(cwd) / "Obfuscator/bin/Release/net10.0"
                output.mkdir(parents=True, exist_ok=True)
                (output / "obfuscator.dll").write_bytes(b"tool")
            if "build" in command and str(command[2]).endswith("plugin.csproj"):
                self._write_built_plugin(cwd)
            return "", ""

        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            self._write_project(root, "Agent.Models")
            self._write_project(root, "plugin")
            self._compile_in_workspace(
                Path(os.path.relpath(root)), capture, False
            )

        build = next(
            item for item in commands
            if "build" in item and str(item[2]).endswith("Obfuscator.csproj")
        )
        self.assertTrue(Path(build[2]).is_absolute())

    def test_obfuscated_multi_file_plugin_renames_assembly_identity(self):
        payload, commands = self._compile_obfuscated_plugin(False)

        self.assertEqual(b"plugin", payload)
        rewrite = next(item for item in commands if "rewrite-source" in item)
        il_batch = next(item for item in commands if "rewrite-il-batch" in item)
        self.assertEqual(rewrite[rewrite.index("--seed") + 1],
                         il_batch[il_batch.index("--seed") + 1])
        self.assertEqual("37eb846a-12b9-45d5-a49c-8e10754cc0ba",
                         rewrite[rewrite.index("--uuid") + 1])
        self.assertIn("--skip-file-rename", il_batch)
        self.assertNotIn("--skip-assembly-rename", il_batch)

    def test_obfuscated_single_file_plugin_renames_assembly_identity(self):
        payload, commands = self._compile_obfuscated_plugin(True)

        self.assertEqual(b"plugin", payload)
        il_batch = next(item for item in commands if "rewrite-il-batch" in item)
        self.assertIn("--skip-file-rename", il_batch)
        self.assertNotIn("--skip-assembly-rename", il_batch)

    def test_obfuscated_plugin_applies_payload_semantic_rename_pass(self):
        _, commands = self._compile_obfuscated_plugin(True)

        rewrite = next(item for item in commands if "rewrite-source" in item)
        self.assertIn("--broad-semantic-rename", rewrite)
        project_index = rewrite.index("--project-root") + 1
        self.assertEqual("plugin/plugin.csproj", rewrite[project_index])
        self.assertEqual("Release", rewrite[rewrite.index("--configuration") + 1])

    def test_obfuscated_plugin_allowlists_exact_effective_assembly_names(self):
        def project(name):
            return (
                "<Project><PropertyGroup><AssemblyName>"
                + name
                + "</AssemblyName></PropertyGroup></Project>"
            )

        _, commands = self._compile_obfuscated_plugin(
            False,
            plugin_project=project("Explicit.Plugin"),
            models_project=project("Contracts.Models"),
        )

        allowed = self._first_party_assemblies(commands)
        self.assertEqual(["Contracts.Models", "Explicit.Plugin"], allowed)
        self.assertNotIn("37eb846a-12b9-45d5-a49c-8e10754cc0ba", allowed)

    def test_contract_fingerprint_normalizes_uuid(self):
        self.assertEqual(
            "6f1002bf3deabf006a9caff07d53d12a8ebcd92dfcf60adb8ba0b0ac844e627b",
            load_module.derive_contract_fingerprint(
                "{37EB846A-12B9-45D5-A49C-8E10754CC0BA}"
            ),
        )

    def test_obfuscated_plugin_temp_source_contains_only_contract_fingerprint(self):
        payload_uuid = "37eb846a-12b9-45d5-a49c-8e10754cc0ba"
        with tempfile.TemporaryDirectory() as root:
            source = load_module.write_contract_metadata_source(root, payload_uuid)
            contents = source.read_text()

        self.assertIn("AthenaPluginContract", contents)
        self.assertIn(load_module.derive_contract_fingerprint(payload_uuid), contents)
        self.assertNotIn(payload_uuid, contents)

    def test_compile_plugin_passes_payload_single_file_mode(self):
        for single_file in (False, True):
            with self.subTest(single_file=single_file):
                command = load_module.LoadCommand()
                command.compile_command = mock.AsyncMock(return_value=b"plugin")
                task_data = types.SimpleNamespace(
                    Payload=types.SimpleNamespace(UUID="payload-uuid"),
                    BuildParameters=[
                        types.SimpleNamespace(Name="obfuscate", Value=True),
                        types.SimpleNamespace(
                            Name="single-file", Value=single_file
                        ),
                    ],
                )
                with tempfile.TemporaryDirectory() as root:
                    plugin = Path(root) / "plugin"
                    plugin.mkdir()
                    result = asyncio.run(command._compile_plugin(
                        task_data, "plugin", plugin, Path(root) / "missing"
                    ))

                self.assertEqual(b"plugin", result)
                command.compile_command.assert_awaited_once_with(
                    str(plugin), "payload-uuid", True, single_file
                )

    def test_obfuscated_plugin_copies_and_allowlists_sibling_project_references(self):
        plugin_project = (
            "<Project><ItemGroup>"
            '<ProjectReference Include="..\\Agent.Models\\Agent.Models.csproj" />'
            '<ProjectReference Include="..\\Agent.Managers.Windows\\Agent.Managers.Windows.csproj" />'
            "</ItemGroup></Project>"
        )
        commands = []
        copied_siblings = []

        async def capture(command, cwd):
            commands.append(command)
            if "rewrite-source" in command:
                temp_root = Path(command[command.index("--input") + 1])
                copied_siblings.append(
                    (temp_root / "Agent.Managers.Windows/Agent.Managers.Windows.csproj").is_file()
                )
            if "build" in command and str(command[2]).endswith("plugin.csproj"):
                self._write_built_plugin(cwd)
            return "", ""

        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            self._init_obfuscated_workspace(root, plugin_project)
            self._write_project(root, "Agent.Managers.Windows")
            self._compile_in_workspace(root, capture)

        self.assertEqual([True], copied_siblings)
        self.assertEqual(
            ["Agent.Managers.Windows", "Agent.Models", "plugin"],
            self._first_party_assemblies(commands),
        )

    def test_obfuscated_plugin_persists_platform_dependency_dlls(self):
        async def capture(command, cwd):
            if "build" in command and str(command[2]).endswith("plugin.csproj"):
                self._write_built_plugin(cwd)
                common = Path(cwd).parent / "bin/common"
                common.mkdir(parents=True, exist_ok=True)
                (common / "Renci.SshNet.dll").write_bytes(b"sshnet")
            return "", ""

        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            self._init_obfuscated_workspace(root)
            self._compile_in_workspace(root, capture)
            persisted = (root / "bin/common/Renci.SshNet.dll").read_bytes()

        self.assertEqual(b"sshnet", persisted)


if __name__ == "__main__":
    unittest.main()


