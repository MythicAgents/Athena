from .athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class UacCheckArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class UacCheckCommand(CommandBase):
    cmd = "uac-check"
    needs_admin = False
    script_only = True
    depends_on = None
    plugin_libraries = []
    help_cmd = "uac-check"
    description = "Check UAC policy settings via registry (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = UacCheckArguments
    attackmapping = ["T1548.002"]
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
                "key": "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System",
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
