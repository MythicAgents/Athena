from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class PrivcheckArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class PrivcheckCommand(CommandBase):
    cmd = "privcheck"
    needs_admin = False
    script_only = True
    depends_on = "privesc"
    plugin_libraries = []
    help_cmd = "privcheck"
    description = "Check current token privileges and integrity level (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = PrivcheckArguments
    attackmapping = ["T1078.003"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows]
    )
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="privesc",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"action": "privcheck"})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
