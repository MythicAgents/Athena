from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class ArpArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="cidr",
                type=ParameterType.String,
                description="The CIDR to scan",
            ),
        ]

    async def parse_arguments(self):
        pass


class ArpCommand(CommandBase):
    cmd = "arp"
    needs_admin = False
    help_cmd = "arp <cidr>"
    description = "Perform an ARP scan against a host"
    version = 1
    author = "@checkymander"
    attackmapping = []
    argument_class = ArpArguments
    attributes = CommandAttributes(
        load_only=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("cidr", self.command_line)
        else:
            raise ValueError("Missing arguments")

    async def process_response(self, response: AgentResponse):
        pass
