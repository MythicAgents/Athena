import ast
from pathlib import Path
from unittest import TestCase


class BuiltinAssemblyObfuscationTests(TestCase):
    @classmethod
    def setUpClass(cls):
        cls.builder_py = (Path(__file__).parents[1] / ".." / "mythic" / "agent_functions" / "builder.py").resolve()
        cls.source = cls.builder_py.read_text()
        cls.tree = ast.parse(cls.source)

    def method_source(self, name):
        node = next(node for node in ast.walk(self.tree) if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)) and node.name == name)
        return ast.get_source_segment(self.source, node)

    def test_published_payload_assemblies_are_batch_renamed(self):
        source = self.method_source("obfuscate_published_assemblies")
        self.assertIn("patch-bundle", source)
        self.assertIn("rewrite-dir", source)
        self.assertIn("derive_obfuscation_seed", source)

    def test_payload_build_runs_batch_rename_after_publish(self):
        source = self.method_source("build")
        self.assertIn("obfuscate_published_assemblies", source)
