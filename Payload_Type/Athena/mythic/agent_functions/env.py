from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class IfconfigArguments(TaskArguments):
    def __init__(self, command_line):
        super().__init__(command_line)
        self.args = {}

    async def parse_arguments(self):
        pass


class IfconfigCommand(CommandBase):
    cmd = "env"
    needs_admin = False
    help_cmd = "env"
    description = "output current environment variables"
    version = 1
    author = "@tr41nwr3ck"
    attackmapping = []
    argument_class = IfconfigArguments

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        resp = await MythicRPC().execute("create_artifact", task_id=task.id,
            artifact="$.NSHost.currentHost.addresses",
            artifact_type="API Called",
        )
        return task

    async def process_response(self, response: AgentResponse):
        pass
