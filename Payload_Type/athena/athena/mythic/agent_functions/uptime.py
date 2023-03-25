from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_messages import message_converter


class UptimeArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
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
    #attackmapping = ["T1592"]
    attackmapping = []
    argument_class = UptimeArguments
    attributes = CommandAttributes(
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
