from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class UnlinkArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class UnlinkCommand(CommandBase):
    cmd = "unlink"
    needs_admin = False
    help_cmd = "unlink"
    description = "Unlink from the existing forwarder"
    version = 1
    author = "@checkymander"
    attackmapping = []
    argument_class = UnlinkArguments
    attributes = CommandAttributes(
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
