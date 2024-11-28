from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *

class EchoArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class EchoCommand(CommandBase):
    cmd = "echo"
    needs_admin = False
    help_cmd = "echo"
    description = "Starts an interactive echo session with the agent."
    version = 1
    author = "@checkymander"
    supported_ui_features = ["task_response:interactive"]
    argument_class = EchoArguments
    attackmapping = ["T1005", "T1552.001"]
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