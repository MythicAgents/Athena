from mythic_payloadtype_container.MythicCommandBase import *
import json


class LinkArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="The host to connect to.",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            CommandParameter(
                name="pipename",
                type=ParameterType.String,
                description="THe name of the pipe the agent is listening on.",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hostname", self.command_line.split()[0])
                self.add_arg("pipename", self.command_line.split()[1])


class LinkCommand(CommandBase):
    cmd = "link"
    needs_admin = False
    help_cmd = "link <hostname> <pipename>"
    description = "Initiate a connection with a SMB Athena agent."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = True
    is_upload_file = False
    author = "@checkymander"
    argument_class = LinkArguments
    attackmapping = []
    attributes = CommandAttributes(
        load_only=False,
        builtin=True,
        supported_os=[SupportedOS.Windows]
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass