from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class BitsTransferArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["download", "list"],
                default_value="download",
                description="BITS operation",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="url", cli_name="url",
                display_name="URL",
                type=ParameterType.String,
                description="Download URL",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="path", cli_name="path",
                display_name="Destination Path",
                type=ParameterType.String,
                description="Local file path for download",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class BitsTransferCommand(CommandBase):
    cmd = "bits-transfer"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "bits-transfer -action download -url https://example.com/file -path C:\\temp\\file"
    description = "BITS file transfer (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = BitsTransferArguments
    attackmapping = ["T1197"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
