from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import base64
import json

from .athena_messages import message_converter


class JobsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
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
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task


    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp

