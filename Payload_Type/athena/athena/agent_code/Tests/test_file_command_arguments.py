import asyncio
import unittest

try:
    from mythic_test_bootstrap import load_command
except ModuleNotFoundError:
    from .mythic_test_bootstrap import load_command


cp_module = load_command("cp.py")
mv_module = load_command("mv.py")
zip_module = load_command("zip.py")
zip_dl_module = load_command("zip-dl.py")
zip_inspect_module = load_command("zip-inspect.py")


class FileCommandArgumentTests(unittest.TestCase):
    def parse(self, argument_class, command_line):
        arguments = argument_class(command_line)
        asyncio.run(arguments.parse_arguments())
        return arguments

    def assert_serialized(self, argument_class, command_line, expected):
        arguments = self.parse(argument_class, command_line)
        self.assertEqual(expected, arguments.serialize())

    def test_cp_parses_working_command(self):
        self.assert_serialized(
            cp_module.CpArguments,
            '"C:\\Program Files\\source.txt" "D:\\Archive\\target.txt"',
            '{"destination": "D:\\\\Archive\\\\target.txt", "source": "C:\\\\Program Files\\\\source.txt"}',
        )

    def test_mv_parses_working_command(self):
        self.assert_serialized(
            mv_module.MvArguments,
            '"C:\\Program Files\\source.txt" "D:\\Archive\\target.txt"',
            '{"destination": "D:\\\\Archive\\\\target.txt", "source": "C:\\\\Program Files\\\\source.txt"}',
        )

    def test_zip_parses_working_command(self):
        self.assert_serialized(
            zip_module.ZipArguments,
            '"C:\\Data Files" "D:\\Archive\\data.zip"',
            '{"destination": "D:\\\\Archive\\\\data.zip", "source": "C:\\\\Data Files"}',
        )

    def test_zip_inspect_parses_working_command(self):
        self.assert_serialized(
            zip_inspect_module.ZipInspectArguments,
            '"C:\\Archive Files\\data.zip"',
            '{"path": "C:\\\\Archive Files\\\\data.zip"}',
        )

    def test_zip_dl_parses_working_command(self):
        self.assert_serialized(
            zip_dl_module.ZipDlArguments,
            '"C:\\Data Files" "D:\\Archive\\data.zip"',
            '{"destination": "D:\\\\Archive\\\\data.zip", "force": false, "source": "C:\\\\Data Files", "write": true}',
        )


if __name__ == "__main__":
    unittest.main()
