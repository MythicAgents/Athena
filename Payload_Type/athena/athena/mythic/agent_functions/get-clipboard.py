from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *

class GetClipboardArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass


class GetClipboardCommand(CommandBase):
    cmd = "get-clipboard"
    needs_admin = False
    help_cmd = "get-clipboard"
    description = "Tasks Athena to return the contents of the clipboard."
    version = 1
    supported_ui_features = []
    author = "@checkymander"
    attackmapping = ["T1115"]
    argument_class = GetClipboardArguments
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows, SupportedOS.MacOS]
    )
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass

