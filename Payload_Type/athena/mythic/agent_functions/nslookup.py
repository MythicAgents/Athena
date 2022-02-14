from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class NslookupArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="hosts",
                type=ParameterType.String,
                description="Comma separate list of hosts",
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


class CatCommand(CommandBase):
    cmd = "nslookup"
    needs_admin = False
    help_cmd = "nslookup DC1.gaia.local,FS1.gaia.local,gaia.local"
    description = "Perform an nslookup on the provided hosts"
    version = 1
    author = "@checkymander"
    argument_class = NslookupArguments
    attackmapping = ["T1106"]
    attributes = CommandAttributes(
        load_only=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
