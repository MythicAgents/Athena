from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_utils import message_converter

class CaffeinateArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass


class CaffeinateCommand(CommandBase):
    cmd = "caffeinate"
    needs_admin = False
    help_cmd = "caffeinate"
    description = "Keep the computer awake, or let the computer sleep if it's already caffeinated"
    version = 1
    author = "@checkymander"
    argument_class = CaffeinateArguments
    attackmapping = ["T1005", "T1552.001"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass