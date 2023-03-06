from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class HostnameArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class HostnameCommand(CommandBase):
    cmd = "hostname"
    needs_admin = False
    help_cmd = "hostname"
    description = "Tasks Athena to return any remaining task output and exit."
    version = 1
    author = "@checkymander"
    #attackmapping = ["T1082"]
    attackmapping = []
    argument_class = HostnameArguments
    attributes = CommandAttributes(
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

