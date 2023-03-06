from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class PwdArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class PwdCommand(CommandBase):
    cmd = "pwd"
    needs_admin = False
    help_cmd = "pwd"
    description = "Tasks Athena to display the current working directory."
    version = 1
    author = "@checkymander"
    #attackmapping = ["T1083"]
    attackmapping = []
    argument_class = PwdArguments
    attributes = CommandAttributes(
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

