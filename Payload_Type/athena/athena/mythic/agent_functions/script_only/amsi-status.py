from .athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class AmsiStatusArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class AmsiStatusCommand(CommandBase):
    cmd = "amsi-status"
    needs_admin = False
    script_only = True
    depends_on = None
    plugin_libraries = []
    help_cmd = "amsi-status"
    description = "Check AMSI provider status via registry (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = AmsiStatusArguments
    attackmapping = ["T1562.001"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="reg",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "query",
                "hive": "HKLM",
                "key": "SOFTWARE\\Microsoft\\AMSI\\Providers",
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
