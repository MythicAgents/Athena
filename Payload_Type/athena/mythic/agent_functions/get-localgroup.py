from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class GetSharesArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                default_value="",
                description="Server to scan",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    group_name="Default"
                )]
            ),
            CommandParameter(
                name="group",
                type=ParameterType.String,
                default_value="",
                description="Group to enumerate",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    group_name="Default"
                )]
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hostname", self.command_line.split()[0])
                self.add_arg("group", self.command_line.split()[1])


class GetSharesCommand(CommandBase):
    cmd = "get-localgroup"
    needs_admin = False
    help_cmd = "get-localgroup [-server <servername] [-group <groupname>]"
    description = "Get localgroups on a host, or members of a group if specified."
    version = 1
    author = "@checkymander"
    argument_class = GetSharesArguments
    attackmapping = ["T1106"]
    attributes = CommandAttributes(
        load_only=True,
        supported_os=[SupportedOS.Windows],
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
       return task

    async def process_response(self, response: AgentResponse):
        pass
