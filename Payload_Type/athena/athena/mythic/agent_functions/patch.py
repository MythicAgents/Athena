from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class PatchArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class PatchCommand(CommandBase):
    cmd = "patch"
    needs_admin = False
    help_cmd = "patch"
    description = "Run an amsi bypass"
    version = 1
    author = "@checkymander"
    attackmapping = []
    argument_class = PatchArguments
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
