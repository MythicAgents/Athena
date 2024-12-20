from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *

class UptimeArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass


class UptimeCommand(CommandBase):
    cmd = "uptime"
    needs_admin = False
    help_cmd = "uptime"
    description = "output the current uptime in D:H:M:S"
    version = 1
    author = "@tr41nwr3ck"
    attackmapping = ["T1124", "T1082"]
    argument_class = UptimeArguments
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