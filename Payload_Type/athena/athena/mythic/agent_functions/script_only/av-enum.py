from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json


class AvEnumArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass


class AvEnumCommand(CommandBase):
    cmd = "av-enum"
    needs_admin = False
    script_only = True
    depends_on = "wmi"
    plugin_libraries = []
    help_cmd = "av-enum"
    description = "Enumerate AV/antispyware products via WMI"
    version = 1
    author = "@checkymander"
    argument_class = AvEnumArguments
    attackmapping = ["T1518.001"]
    attributes = CommandAttributes(supported_os=[SupportedOS.Windows])
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID, CommandName="wmi",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"action": "av-enum"}))
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
