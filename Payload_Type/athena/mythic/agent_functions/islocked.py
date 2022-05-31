from mythic_payloadtype_container.MythicCommandBase import *
from mythic_payloadtype_container.MythicRPC import *
import base64
import json


class IsLockedArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class IsLockedCommand(CommandBase):
    cmd = "islocked"
    needs_admin = False
    help_cmd = "islocked"
    description = "This lists whether the current user has locked their PC or not."
    version = 1
    author = "@tr41nwr3ck"
    parameters = []
    attackmapping = []
    argument_class = IsLockedArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

