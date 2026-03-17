from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class LoginItemsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class LoginItemsCommand(CommandBase):
    cmd = "login-items"
    needs_admin = False
    script_only = True
    depends_on = "jxa"
    plugin_libraries = []
    help_cmd = "login-items"
    description = "Enumerate login items via JXA (macOS only)"
    version = 1
    author = "@checkymander"
    argument_class = LoginItemsArguments
    attackmapping = ["T1547.015"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.MacOS],
    )
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        jxa_code = (
            "var sys = Application('System Events');"
            "var items = sys.loginItems();"
            "var result = items.map(function(i) {"
            "  return {name: i.name(), path: i.path(), hidden: i.hidden()};"
            "});"
            "JSON.stringify(result);"
        )
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="jxa",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"code": jxa_code})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
