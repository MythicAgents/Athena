from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from Payload_Type.athena.athena.mythic.agent_functions.athena_messages import message_converter


class WinEnumResourcesArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class WinEnumResourcesCommand(CommandBase):
    cmd = "win-enum-resources"
    needs_admin = False
    help_cmd = "win-enum-resources"
    description = "Tasks Athena to use the WinEnumResources NT API call to identify resources on the local network"
    version = 1
    author = "@checkymander"
    #attackmapping = ["T1589"]
    attackmapping = []
    argument_class = WinEnumResourcesArguments
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        user_output = response["message"]
        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))
        return resp

