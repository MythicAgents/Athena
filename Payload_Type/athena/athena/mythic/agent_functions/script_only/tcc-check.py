from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class TccCheckArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class TccCheckCommand(CommandBase):
    cmd = "tcc-check"
    needs_admin = False
    script_only = True
    depends_on = "file-utils"
    plugin_libraries = []
    help_cmd = "tcc-check"
    description = "Check TCC (Transparency, Consent, Control) database (macOS only)"
    version = 1
    author = "@checkymander"
    argument_class = TccCheckArguments
    attackmapping = ["T1069"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="file-utils",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "cat",
                "path": "/Library/Application Support/com.apple.TCC/TCC.db"
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
