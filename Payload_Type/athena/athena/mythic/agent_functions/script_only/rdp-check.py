from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class RdpCheckArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class RdpCheckCommand(CommandBase):
    cmd = "rdp-check"
    needs_admin = False
    script_only = True
    depends_on = "recon"
    plugin_libraries = []
    help_cmd = "rdp-check"
    description = "Check RDP configuration and status (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = RdpCheckArguments
    attackmapping = ["T1021.001"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="recon",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"action": "rdp-check"})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
