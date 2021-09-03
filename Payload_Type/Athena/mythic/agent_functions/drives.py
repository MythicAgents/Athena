from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class DrivesArguments(TaskArguments):
    def __init__(self, command_line):
        super().__init__(command_line)
        self.args = {}

    async def parse_arguments(self):
        pass


class DrivesCommand(CommandBase):
    cmd = "drives"
    needs_admin = False
    help_cmd = "drives"
    description = "Get all drives on the host and there stats "
    version = 1
    author = "@tr41nwr3ck"
    attackmapping = []
    argument_class = DrivesArguments

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
