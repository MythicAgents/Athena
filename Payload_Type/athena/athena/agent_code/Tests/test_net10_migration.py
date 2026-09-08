import os
import importlib.util
import pathlib
import unittest
from types import SimpleNamespace
from unittest import mock


AGENT_CODE = pathlib.Path(__file__).resolve().parents[1]
REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[5]
BUILD_UTILS_PATH = AGENT_CODE / "build_utils.py"


def load_build_utils():
    spec = importlib.util.spec_from_file_location("net10_build_utils", BUILD_UTILS_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError("Unable to load build_utils.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class Net10MigrationTests(unittest.TestCase):
    def test_runtime_directory_selects_highest_numeric_dotnet_10_runtime(self):
        build_utils = load_build_utils()
        runtimes = "\n".join(
            (
                "Microsoft.NETCore.App 9.0.10 [/dotnet/shared/Microsoft.NETCore.App]",
                "Microsoft.NETCore.App 10.0.1 [/dotnet/shared/Microsoft.NETCore.App]",
                "Microsoft.NETCore.App 10.0.11 [/dotnet/shared/Microsoft.NETCore.App]",
            )
        )
        with mock.patch.object(
            build_utils.subprocess,
            "run",
            return_value=SimpleNamespace(stdout=runtimes),
        ):
            selected = build_utils.get_dotnet_directory()

        self.assertEqual(
            "/dotnet/shared/Microsoft.NETCore.App/10.0.11",
            selected,
        )

    def test_runtime_directory_rejects_installations_without_dotnet_10(self):
        build_utils = load_build_utils()
        with mock.patch.object(
            build_utils.subprocess,
            "run",
            return_value=SimpleNamespace(
                stdout=(
                    "Microsoft.NETCore.App 9.0.10 "
                    "[/dotnet/shared/Microsoft.NETCore.App]"
                )
            ),
        ):
            with self.assertRaisesRegex(
                RuntimeError,
                r"Microsoft\.NETCore\.App 10 runtime was not found",
            ):
                build_utils.get_dotnet_directory()

    def test_all_agent_projects_target_net10(self):
        projects = []
        for root, directories, files in os.walk(AGENT_CODE):
            directories[:] = [
                directory
                for directory in directories
                if directory.lower() not in {"bin", "obj", ".vs"}
            ]
            projects.extend(
                pathlib.Path(root) / filename
                for filename in files
                if filename.endswith(".csproj")
            )
        projects.sort()
        self.assertGreater(len(projects), 0)
        stale = [
            str(project.relative_to(AGENT_CODE))
            for project in projects
            if "<TargetFramework>net10.0</TargetFramework>" not in project.read_text()
        ]
        self.assertEqual([], stale)

    def test_ci_installs_dotnet_10(self):
        workflow = (REPOSITORY_ROOT / ".github/workflows/dotnet-desktop.yml").read_text()
        self.assertNotIn("dotnet-version: 8.", workflow)
        self.assertIn("dotnet-version: 10.0.x", workflow)

    def test_ci_runs_uuid_obfuscation_contract_suites(self):
        workflow = (REPOSITORY_ROOT / ".github/workflows/dotnet-desktop.yml").read_text()
        self.assertIn("- Obfuscator.Tests", workflow)
        self.assertIn("- PluginContract.Tests", workflow)
        self.assertIn('Tests/${{ matrix.project }}/${{ matrix.project }}.csproj', workflow)

    def test_container_installs_and_builds_dotnet_10_tooling(self):
        dockerfile = (
            REPOSITORY_ROOT / "Payload_Type/athena/.docker/Dockerfile"
        ).read_text()
        self.assertNotIn("dotnet-install.sh --version 8.", dockerfile)
        self.assertIn("dotnet-install.sh --version 10.0.400", dockerfile)
        self.assertIn("agent_code/Obfuscator/Obfuscator.csproj", dockerfile)

    def test_runtime_output_paths_use_net10(self):
        for relative_path in (
            "Payload_Type/athena/athena/agent_code/build_utils.py",
            "Payload_Type/athena/athena/mythic/agent_functions/builder.py",
            "Payload_Type/athena/athena/mythic/agent_functions/load.py",
        ):
            contents = (REPOSITORY_ROOT / relative_path).read_text()
            self.assertNotIn("net8.0", contents, relative_path)
            self.assertIn("net10.0", contents, relative_path)
        build_utils = (
            REPOSITORY_ROOT
            / "Payload_Type/athena/athena/agent_code/build_utils.py"
        ).read_text()
        self.assertNotIn('"Microsoft.NETCore.App", "8.', build_utils)
        self.assertIn('"dotnet", "--list-runtimes"', build_utils)


if __name__ == "__main__":
    unittest.main()
