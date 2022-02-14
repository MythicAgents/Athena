from mythic_payloadtype_container.MythicCommandBase import *
from mythic_payloadtype_container.MythicRPC import *
import base64
import json


class JobsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class JobsCommand(CommandBase):
    cmd = "jobs"
    needs_admin = False
    help_cmd = "jobs."
    description = "This lists the currently active jobs on the agent."
    version = 1
    author = "@checkymander"
    parameters = []
    attackmapping = []
    argument_class = JobsArguments
    browser_script = [BrowserScript(script_name="jobs", author="@checkymander", for_new_ui=True)]
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task



    async def process_response(self, response: AgentResponse):
        pass

