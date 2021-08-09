from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *
import base64


class JobsArguments(TaskArguments):
    def __init__(self, command_line):
        super().__init__(command_line)
        self.args = {}

    async def parse_arguments(self):
        pass


class JobsCommand(CommandBase):
    cmd = "jobs"
    needs_admin = False
    help_cmd = "jobs."
    description = "This lists the currently active jobs on the agent."
    version = 1
    author = "@checkymander"
    parameters = []
    attackmapping = []
    argument_class = JobsArguments

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task



    async def process_response(self, response: AgentResponse):
        pass

