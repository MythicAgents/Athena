from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class UnloadArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class UnloadCommand(CommandBase):
    cmd = "unload"
    needs_admin = False
    help_cmd = "unload"
    description = "Tasks Athena to unload all loaded commands."
    version = 1
    author = "@checkymander"
    attackmapping = ["T1589"]
    argument_class = UnloadArguments
    attributes = CommandAttributes(
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

