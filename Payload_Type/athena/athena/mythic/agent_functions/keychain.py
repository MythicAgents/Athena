from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class KeychainArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class KeychainCommand(CommandBase):
    cmd = "keychain"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "keychain"
    description = "Enumerate keychain items (macOS only)"
    version = 2
    author = "@checkymander"
    argument_class = KeychainArguments
    attackmapping = ["T1555.001"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
