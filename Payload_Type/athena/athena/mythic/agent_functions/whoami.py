from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class WhoamiArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class WhoamiCommand(CommandBase):
    cmd = "whoami"
    needs_admin = False
    help_cmd = "whoami"
    description = "Tasks Athena to display the current user context."
    version = 1
    author = "@checkymander"
    attackmapping = ["T1589"]
    argument_class = WhoamiArguments
    attributes = CommandAttributes(
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

