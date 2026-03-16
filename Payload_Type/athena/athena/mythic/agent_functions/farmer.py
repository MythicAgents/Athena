from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *

class FarmerArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="port",
                type=ParameterType.Number,
                description="The port to run on",
                default_value=7777,
            ),
            CommandParameter(
                name="downgrade",
                type=ParameterType.Boolean,
                description="Attempt NTLMv1 downgrade (omits NEGOTIATE_EXTENDED_SESSIONSECURITY). Falls back to NTLMv2 if client policy requires it.",
                default_value=False,
            ),
            CommandParameter(
                name="serverHeader",
                type=ParameterType.String,
                description="HTTP Server header value",
                default_value="Microsoft-IIS/10.0",
            ),
            CommandParameter(
                name="bindAddress",
                type=ParameterType.String,
                description="IP address to bind to (default: all interfaces)",
                default_value="",
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("port", self.command_line.split()[0])
        else:
            raise ValueError("Missing arguments")


class FarmerCommand(CommandBase):
    cmd = "farmer"
    needs_admin = False
    help_cmd = "farmer"
    description = "Farmer is a project for collecting NetNTLM hashes in a Windows domain."
    help_cmd = """
Farmer https://github.com/mdsecactivebreach/Farmer
    created by @domchell

Farmer acts as a WebDAV server to catch NetNTLM Authentication hashes from Windows clients.

The server listens on the specified port and responds with a 401 Unauthorized + NTLM challenge.
Uses a random challenge per session (not the default 1122334455667788 IoC).

Options:
  -port           Port to listen on (default: 7777)
  -downgrade      Attempt NTLMv1 downgrade for easier cracking (hashcat 5500 vs 5600).
                  Falls back gracefully to NTLMv2 if client policy requires it.
  -serverHeader   HTTP Server header (default: Microsoft-IIS/10.0)
  -bindAddress    Bind to specific interface (default: all)

Usage: farmer -port 8080 -downgrade true

    """
    version = 2
    author = "@domchell, @checkymander"
    argument_class = FarmerArguments
    attackmapping = ["T1187"]
    attributes = CommandAttributes(
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        downgrade = taskData.args.get_arg("downgrade")
        port = taskData.args.get_arg("port")
        parts = [f"-port {port}"]
        if downgrade:
            parts.append("-downgrade")
        bind = taskData.args.get_arg("bindAddress")
        if bind:
            parts.append(f"-bindAddress {bind}")
        response.DisplayParams = " ".join(parts)
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
