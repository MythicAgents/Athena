from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *


class DrivesArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass


class DrivesCommand(CommandBase):
    cmd = "drives"
    needs_admin = False
    help_cmd = "drives"
    description = "Get all drives on the host and information about them "
    version = 1
    author = "@tr41nwr3ck"
    attackmapping = ["T1083", "T1082"]
    argument_class = DrivesArguments
    browser_script = BrowserScript(script_name="drives", author="@tr41nwr3ck")
    attributes = CommandAttributes(
    )
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
