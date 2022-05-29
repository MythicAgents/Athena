from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class EtwArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class EtwCommand(CommandBase):
    cmd = "etw"
    needs_admin = False
    help_cmd = "etw"
    description = "Tasks Athena to display the current working directory."
    version = 1
    author = "@checkymander"
    attackmapping = ["T1083"]
    argument_class = EtwArguments
    attributes = CommandAttributes(
        load_only=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

