from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class ListLinksArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class ListLinksCommand(CommandBase):
    cmd = "list-links"
    needs_admin = False
    help_cmd = "list-links"
    description = "Tasks Athena to list associated p2p links"
    version = 1
    author = "@checkymander"
    #attackmapping = ["T1082"]
    attackmapping = []
    argument_class = ListLinksArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

