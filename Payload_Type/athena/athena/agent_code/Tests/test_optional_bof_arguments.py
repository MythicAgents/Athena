import asyncio
import unittest

try:
    from mythic_test_bootstrap import load_command
except ModuleNotFoundError:
    from .mythic_test_bootstrap import load_command


klist_module = load_command("outflank_bofs/klist.py")
windowlist_module = load_command("trusted_sec_bofs/windowlist.py")
password_policy_module = load_command("trusted_sec_bofs/get-password-policy.py")


class OptionalBofArgumentTests(unittest.TestCase):
    def assert_accepts_empty_command_line(self, argument_class):
        arguments = argument_class("")
        asyncio.run(arguments.parse_arguments())

    def test_klist_accepts_empty_command_line(self):
        self.assert_accepts_empty_command_line(klist_module.KListArguments)

    def test_windowlist_accepts_empty_command_line(self):
        self.assert_accepts_empty_command_line(windowlist_module.WindowlistArguments)

    def test_get_password_policy_accepts_empty_command_line(self):
        self.assert_accepts_empty_command_line(password_policy_module.GetPasswordPolicyArguments)

    def test_optional_bof_arguments_still_parse_json(self):
        cases = (
            (klist_module.KListArguments, '{"purge": true}', "purge", True),
            (windowlist_module.WindowlistArguments, '{"all": true}', "all", True),
            (
                password_policy_module.GetPasswordPolicyArguments,
                '{"hostname": "dc01.example.test"}',
                "hostname",
                "dc01.example.test",
            ),
        )

        for argument_class, command_line, argument_name, expected in cases:
            with self.subTest(argument_class=argument_class.__name__):
                arguments = argument_class(command_line)
                asyncio.run(arguments.parse_arguments())
                self.assertEqual(expected, arguments.get_arg(argument_name))


if __name__ == "__main__":
    unittest.main()
