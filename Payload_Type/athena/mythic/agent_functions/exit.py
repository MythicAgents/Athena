from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class ExitArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class ExitCommand(CommandBase):
    cmd = "exit"
    needs_admin = False
    help_cmd = "exit"
    description = "Tasks Athena to return any remaining task output and exit."
    version = 1
    supported_ui_features = ["callback_table:exit"]
    is_exit = True
    author = "@checkymander"
    attackmapping = []
    argument_class = ExitArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

