from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_messages import message_converter

class ResetALCArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass

class ResetALCCommand(CommandBase):
    cmd = "reset-assembly-context"
    needs_admin = False
    help_cmd = "reset-assembly-context"
    description = "Tasks Athena to reset the assembly load context, clearing out any long running executables and old assemblies"
    version = 1
    supported_ui_features = []
    author = "@checkymander"
    attackmapping = []
    argument_class = ResetALCArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        resp = await MythicRPC().execute("create_artifact", task_id=task.id,
            artifact="$.NSApplication.sharedApplication.terminate",
            artifact_type="API Called",
        )
        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
