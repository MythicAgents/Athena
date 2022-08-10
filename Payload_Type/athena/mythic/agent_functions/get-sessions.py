from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class GetSessionsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="hosts",
                type=ParameterType.String,
                default_value="",
                description="Comma separated list of hosts",
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hosts", self.command_line)
        else:
            raise ValueError("Missing arguments")


class GetSessionsCommand(CommandBase):
    cmd = "get-sessions"
    needs_admin = False
    help_cmd = "get-sessions DC1.gaia.local,FS1.gaia.local,gaia.local"
    description = "Perform an NetSessionEnum on the provided hosts (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = GetSessionsArguments
    attackmapping = ["T1106"]
    attributes = CommandAttributes(
        load_only=True,
        supported_os=[SupportedOS.Windows],
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
