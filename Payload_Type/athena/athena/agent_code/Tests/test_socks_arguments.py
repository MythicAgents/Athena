import types
import unittest

try:
    from mythic_test_bootstrap import load_command
except ModuleNotFoundError:
    from .mythic_test_bootstrap import load_command

module = load_command("socks.py")


class SocksTaskingTests(unittest.IsolatedAsyncioTestCase):
    async def test_socks_start_parses_and_dispatches_working_command(self):
        arguments = module.SocksArguments("start 1080")
        await arguments.parse_arguments()
        task = types.SimpleNamespace(args=arguments, Task=types.SimpleNamespace(ID=71))
        calls = []

        async def proxy_start(message):
            calls.append(message)
            return types.SimpleNamespace(Success=True, Error="")

        module.SendMythicRPCProxyStartCommand = proxy_start
        response = await module.SocksCommand().create_go_tasking(task)

        self.assertEqual('{"action": "start", "port": 1080}', arguments.serialize())
        self.assertEqual(1080, calls[0].LocalPort)
        self.assertEqual("socks", calls[0].PortType)
        self.assertTrue(response.Success)
        self.assertTrue(response.Completed)


if __name__ == "__main__":
    unittest.main()
