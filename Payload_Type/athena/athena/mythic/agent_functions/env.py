from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from Payload_Type.athena.athena.mythic.agent_functions.athena_messages import message_converter


class EnvArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class EnvCommand(CommandBase):
    cmd = "env"
    needs_admin = False
    help_cmd = "env"
    description = "output current environment variables"
    version = 1
    author = "@tr41nwr3ck"
    attackmapping = []
    argument_class = EnvArguments
    browser_script = BrowserScript(script_name="env", author="@tr41nwr3ck")
    attributes = CommandAttributes(
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        user_output = response["message"]
        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))
        return resp
