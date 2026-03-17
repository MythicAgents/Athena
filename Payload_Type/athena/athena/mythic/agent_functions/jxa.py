from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class JxaArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="code", cli_name="code",
                display_name="JXA Code",
                type=ParameterType.String,
                description="JavaScript for Automation code to execute",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("code", self.command_line)

class JxaCommand(CommandBase):
    cmd = "jxa"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "jxa -code \"Application('Finder').name()\""
    description = "Execute JavaScript for Automation (macOS only)"
    version = 2
    author = "@checkymander"
    argument_class = JxaArguments
    attackmapping = ["T1059.007"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.MacOS],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
