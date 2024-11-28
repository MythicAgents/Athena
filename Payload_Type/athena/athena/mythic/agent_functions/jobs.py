from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

class JobsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass


class JobsCommand(CommandBase):
    cmd = "jobs"
    needs_admin = False
    help_cmd = "jobs"
    description = "This lists the currently active jobs on the agent."
    version = 1
    author = "@checkymander"
    attackmapping = []
    argument_class = JobsArguments
    browser_script = BrowserScript(script_name="jobs", author="@checkymander")
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

