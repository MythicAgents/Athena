from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class RdpConfigArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class RdpConfigCommand(CommandBase):
    cmd = "rdp-config"
    needs_admin = False
    script_only = True
    depends_on = None
    plugin_libraries = []
    help_cmd = "rdp-config"
    description = "Check RDP configuration via registry (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = RdpConfigArguments
    attackmapping = ["T1021.001"]
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
                "key": "SYSTEM\\CurrentControlSet\\Control\\Terminal Server",
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
