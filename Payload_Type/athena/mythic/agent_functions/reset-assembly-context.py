from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *

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
    supported_ui_features = ["callback_table:exit"]
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

    async def process_response(self, response: AgentResponse):
        pass
