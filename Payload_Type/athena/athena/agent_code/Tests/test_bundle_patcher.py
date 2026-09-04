from pathlib import Path
from unittest import TestCase


class BundlePatcherTests(TestCase):
    def test_atomic_replacement_preserves_unix_executable_mode(self):
        source = (Path(__file__).parents[1] / "Tools" / "AssemblyNameObfuscator" / "BundlePatcher.cs").read_text()
        self.assertIn("File.GetUnixFileMode", source)
        self.assertIn("File.SetUnixFileMode", source)

    def test_bundle_rewrite_preserves_nested_assembly_paths(self):
        root = Path(__file__).parents[1] / "Tools" / "AssemblyNameObfuscator"
        bundle_source = (root / "BundlePatcher.cs").read_text()
        renamer_source = (root / "AssemblyIdentityRenamer.cs").read_text()
        self.assertIn("relativeDirectory = Path.GetDirectoryName", bundle_source)
        self.assertIn("orig.RelativePath.Replace", bundle_source)
        self.assertIn("SearchOption.AllDirectories", renamer_source)

    def test_batch_rewrite_fails_closed_on_unwritable_assembly(self):
        source = (Path(__file__).parents[1] / "Tools" / "AssemblyNameObfuscator" / "AssemblyIdentityRenamer.cs").read_text()
        self.assertNotIn("catch (AssemblyResolutionException)", source)

    def test_directory_rewrite_preserves_entry_assembly(self):
        source = (Path(__file__).parents[1] / "Tools" / "AssemblyNameObfuscator" / "Program.cs").read_text()
        self.assertIn("extraSkipNames", source)
        self.assertIn("runtimeconfig.json", source)
