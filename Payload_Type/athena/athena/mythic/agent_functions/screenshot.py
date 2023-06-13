from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class ScreenshotArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class ScreenshotCommand(CommandBase):
    cmd = "screenshot"
    needs_admin = False
    help_cmd = "screenshot"
    description = "Tasks Athena to take a screenshot and returns as base64."
    version = 1
    supported_ui_features = []
    is_exit = False
    author = "@tr41nwr3ck"
    attackmapping = []
    argument_class = ScreenshotArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=False,
        supported_os=[SupportedOS.Windows],
    )


    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response


async def process_response(self, response: AgentResponse):
    pass