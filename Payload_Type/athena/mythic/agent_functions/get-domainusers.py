from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class GetDomainUsersArguments(TaskArguments):
    def __init__(self, command_line):
        super().__init__(command_line)
        self.args = {}

    async def parse_arguments(self):
        pass


class GetDomainUsersCommand(CommandBase):
    cmd = "get-domainusers"
    needs_admin = False
    help_cmd = "get-domainusers"
    description = "Tasks Athena to get domain user information from Active Directory."
    version = 1
    supported_ui_features = []
    author = "@checkymander"
    attackmapping = []
    argument_class = GetClipboardArguments
    browser_script = BrowserScript(script_name="get-domainusers", author="@tr41nwr3ck")


    async def create_tasking(self, task: MythicTask) -> MythicTask:
        resp = await MythicRPC().execute("create_artifact", task_id=task.id,
            artifact="$.NSApplication.sharedApplication.terminate",
            artifact_type="API Called",
        )
        return task

    async def process_response(self, response: AgentResponse):
        pass
