from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_messages import message_converter


class PsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class PsCommand(CommandBase):
    cmd = "ps"
    needs_admin = False
    help_cmd = "ps"
    description = "Get a brief process listing with basic information."
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
    #attackmapping = ["T1106"]
    attackmapping = []
    browser_script = BrowserScript(script_name="ps", author="@checkymander")
    attributes = CommandAttributes(
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        user_output = response["message"]
        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))
        return resp