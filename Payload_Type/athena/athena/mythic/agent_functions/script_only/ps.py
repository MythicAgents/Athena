from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class PsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class PsCommand(CommandBase):
    cmd = "ps"
    needs_admin = False
    script_only = True
    depends_on = "proc-enum"
    plugin_libraries = []
    help_cmd = "ps"
    description = (
        "Get a brief process listing with basic information."
    )
    version = 1
    is_exit = False
    is_file_browse = False
    supported_ui_features = ["process_browser:list"]
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    is_process_list = True
    author = "@checkymander"
    argument_class = PsArguments
    attackmapping = ["T1057"]
    browser_script = BrowserScript(
        script_name="ps", author="@checkymander")
    attributes = CommandAttributes()
    completion_functions = {
        "command_callback": default_completion_callback
    }

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="proc-enum",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"action": "ps"})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
