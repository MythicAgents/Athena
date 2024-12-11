from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *

class NetstatArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass


class NetstatCommand(CommandBase):
    cmd = "netstat"
    needs_admin = False
    help_cmd = "jobs"
    description = "Lists endpoints and listening ports"
    version = 1
    author = "@checkymander"
    attackmapping = ["T1049", "T1082"]
    argument_class = NetstatArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=False
    )
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response


    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass

