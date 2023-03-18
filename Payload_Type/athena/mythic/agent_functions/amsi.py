from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class AmsiArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class AmsiCommand(CommandBase):
    cmd = "amsi"
    needs_admin = False
    help_cmd = "amsi"
    description = "Run an amsi bypass"
    version = 1
    author = "@checkymander"
    attackmapping = []
    argument_class = AmsiArguments
    attributes = CommandAttributes(
        load_only=True,
        supported_os=[SupportedOS.Windows],
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
