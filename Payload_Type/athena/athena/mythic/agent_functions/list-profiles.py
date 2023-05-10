from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class ListProfilesArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class ListProfilesCommand(CommandBase):
    cmd = "list-profiles"
    needs_admin = False
    help_cmd = "list-profiles"
    description = "Tasks Athena to list available c2 profiles"
    version = 1
    author = "@checkymander"
    #attackmapping = ["T1082"]
    attackmapping = []
    argument_class = ListProfilesArguments
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

