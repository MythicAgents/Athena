import importlib.util
from pathlib import Path
from unittest import TestCase, mock

BUILD_UTILS = Path(__file__).parents[1] / "build_utils.py"
spec = importlib.util.spec_from_file_location("build_utils", BUILD_UTILS)
build_utils = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build_utils)


class AssemblyIdentityPipelineTests(TestCase):
    def test_identity_rewrite_selects_only_named_assembly(self):
        requested = "echo,jobs,Agent.Profiles.Zoom"
        self.assertTrue(build_utils.should_obfuscate_assembly_identity(requested, "echo"))
        self.assertTrue(build_utils.should_obfuscate_assembly_identity(requested, "Agent.Profiles.Zoom"))
        self.assertFalse(build_utils.should_obfuscate_assembly_identity(requested, "Agent.Models"))
        self.assertFalse(build_utils.should_obfuscate_assembly_identity("", "echo"))

    def test_seed_matches_dev_obfuscator_algorithm(self):
        self.assertEqual(
            546960503,
            build_utils.derive_obfuscation_seed(
                "37eb846a-12b9-45d5-a49c-8e10754cc0ba"
            ),
        )

    @mock.patch.object(build_utils.subprocess, "run")
    def test_identity_rewriter_runs_against_obfuscar_output(self, run):
        assembly = "/tmp/Obfuscated/jobs.dll"

        build_utils.obfuscate_assembly_identity(
            assembly,
            "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
        )

        command = run.call_args.args[0]
        self.assertEqual("dotnet", command[0])
        self.assertTrue(command[1].endswith(
            "Tools/AssemblyNameObfuscator/bin/Release/net8.0/AssemblyNameObfuscator.dll"
        ))
        self.assertEqual(assembly, command[2])
        self.assertEqual("546960503", command[3])
        self.assertTrue(run.call_args.kwargs["check"])
