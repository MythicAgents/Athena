from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

from .athena_utils import message_converter


class EntitlementsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="pid",
                type=ParameterType.Number,
                description="The process to check entitlements for",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line.strip()) == 0:
            raise Exception("run requires a path to an executable to run.\n\tUsage: {}".format(EntitlementsCommand.help_cmd))
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)   
        pass


class EntitlementsCommand(CommandBase):
    cmd = "entitlements -pid 1234"
    needs_admin = False
    help_cmd = "entitlements -pid 1234"
    description = "Get entitlements of a specific process"
    version = 1
    supported_ui_features = ["task_response:interactive"]
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = EntitlementsArguments
    attackmapping = ["T1059", "T1059.004"]
    attributes = CommandAttributes(
        builtin=False,
        supported_os=[SupportedOS.MacOS]
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp