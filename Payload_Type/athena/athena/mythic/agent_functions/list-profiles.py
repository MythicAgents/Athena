from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class ListProfilesArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class ListProfilesCommand(CommandBase):
    cmd = "list-profiles"
    needs_admin = False
    help_cmd = "list-profiles"
    description = "Tasks Athena to list available c2 profiles"
    version = 1
    author = "@checkymander"
    attackmapping = ["T1082"]
    argument_class = ListProfilesArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

