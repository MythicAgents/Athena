import asyncio
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

    def _compile_obfuscated_plugin(
        self, single_file, plugin_project="<Project />",
        models_project="<Project />"
    ):
        commands = []
        payload_uuid = "37eb846a-12b9-45d5-a49c-8e10754cc0ba"

        async def capture(command, cwd):
            commands.append(command)
            if "build" in command and str(command[2]).endswith("plugin.csproj"):
                output = Path(cwd) / "bin/Release/net10.0"
                output.mkdir(parents=True, exist_ok=True)
                (output / "plugin.dll").write_bytes(b"plugin")
            return "", ""

        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            (root / "Agent.Models").mkdir()
            (root / "Agent.Models/Agent.Models.csproj").write_text(models_project)
            plugin = root / "plugin"
            plugin.mkdir()
            (plugin / "plugin.csproj").write_text(plugin_project)
            binary = root / "Obfuscator/bin/Release/net10.0/obfuscator.dll"
            binary.parent.mkdir(parents=True)
            binary.write_bytes(b"tool")

            command = load_module.LoadCommand()
            command.agent_code_path = root
            with mock.patch.object(load_module, "run_checked", capture):
                payload = asyncio.run(command.compile_command(
                    str(plugin), payload_uuid, True, single_file
                ))

        return payload, commands

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

    def test_obfuscated_single_file_plugin_skips_assembly_identity_rename(self):
        payload, commands = self._compile_obfuscated_plugin(True)

        self.assertEqual(b"plugin", payload)
        il_batch = next(item for item in commands if "rewrite-il-batch" in item)
        self.assertIn("--skip-file-rename", il_batch)
        self.assertIn("--skip-assembly-rename", il_batch)

    def test_obfuscated_plugin_allowlists_exact_effective_assembly_names(self):
        project = lambda name: (
            "<Project><PropertyGroup><AssemblyName>"
            + name
            + "</AssemblyName></PropertyGroup></Project>"
        )
        _, commands = self._compile_obfuscated_plugin(
            False,
            plugin_project=project("Explicit.Plugin"),
            models_project=project("Contracts.Models"),
        )

        il_batch = next(item for item in commands if "rewrite-il-batch" in item)
        allowed = [
            il_batch[index + 1]
            for index, value in enumerate(il_batch)
            if value == "--first-party-assembly"
        ]
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


if __name__ == "__main__":
    unittest.main()
