import ast
from pathlib import Path
from unittest import TestCase


class HotLoadObfuscationFlagTests(TestCase):
    def test_compile_command_forwards_payload_obfuscation_setting(self):
        load_py = (
            Path(__file__).parents[1]
            / ".." / "mythic" / "agent_functions" / "load.py"
        ).resolve()
        tree = ast.parse(load_py.read_text())
        compile_command = next(
            node for node in ast.walk(tree)
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef))
            and node.name == "compile_command"
        )
        self.assertIn(
            "/p:ObfuscateAssemblyNames={}",
            ast.get_source_segment(load_py.read_text(), compile_command),
        )

        argument_names = [arg.arg for arg in compile_command.args.args]
        self.assertIn("obfuscate", argument_names)
        self.assertIn(
            "/p:Obfuscate={}",
            ast.get_source_segment(load_py.read_text(), compile_command),
        )
