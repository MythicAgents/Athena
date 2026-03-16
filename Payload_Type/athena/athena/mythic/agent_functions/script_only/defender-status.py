from .athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class DefenderStatusArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class DefenderStatusCommand(CommandBase):
    cmd = "defender-status"
    needs_admin = False
    script_only = True
    depends_on = "wmi"
    plugin_libraries = []
    help_cmd = "defender-status"
    description = "Check antivirus/Defender status via WMI (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = DefenderStatusArguments
    attackmapping = ["T1518.001"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="wmi",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"action": "defender-status"})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
