from mythic_payloadtype_container.MythicCommandBase import *
import json


class TestportArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="hosts",
                type=ParameterType.String,
                description="The hosts to check (comma separated)",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            CommandParameter(
                name="ports",
                type=ParameterType.String,
                description="Toe ports to check (comma separated)",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hosts", self.command_line.split()[0])
                self.add_arg("ports", self.command_line.split()[1])
        else:
            raise ValueError("Missing arguments")


class TestportCommand(CommandBase):
    cmd = "test-port"
    needs_admin = False
    help_cmd = "test-port"
    description = "Check if a list of ports are open against a host/list of hosts."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@checkymander"
    argument_class = TestportArguments
    attackmapping = []
    attributes = CommandAttributes(
        load_only=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass