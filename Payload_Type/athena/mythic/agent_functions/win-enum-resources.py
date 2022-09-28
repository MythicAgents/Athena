from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class WinEnumResourcesArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class WinEnumResourcesCommand(CommandBase):
    cmd = "win-enum-resources"
    needs_admin = False
    help_cmd = "win-enum-resources"
    description = "Tasks Athena to use the WinEnumResources NT API call to identify resources on the local network"
    version = 1
    author = "@checkymander"
    attackmapping = ["T1589"]
    argument_class = WinEnumResourcesArguments
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

