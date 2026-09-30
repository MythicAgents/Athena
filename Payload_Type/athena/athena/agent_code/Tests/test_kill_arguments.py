import asyncio
import unittest

try:
    from mythic_test_bootstrap import ParameterType, load_command
except ModuleNotFoundError:
    from .mythic_test_bootstrap import ParameterType, load_command

kill_module = load_command("kill.py")


class KillArgumentTests(unittest.TestCase):
    def test_kill_parses_working_command(self):
        arguments = kill_module.KillArguments("4242")
        asyncio.run(arguments.parse_arguments())
        self.assertEqual(4242, arguments.get_arg("id"))
        self.assertEqual(ParameterType.Number, arguments.types["id"])
        self.assertEqual('{"id": 4242}', arguments.serialize())


if __name__ == "__main__":
    unittest.main()
