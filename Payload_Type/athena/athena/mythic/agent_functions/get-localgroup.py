from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class GetLocalGroupArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                default_value="",
                description="Server to scan",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    group_name="Default",
                    ui_position = 0,
                )]
            ),
            CommandParameter(
                name="group",
                type=ParameterType.String,
                default_value="",
                description="Group to enumerate",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    group_name="Default",
                    ui_position = 1,
                )]
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hostname", self.command_line.split()[0])
                self.add_arg("group", self.command_line.split()[1])


class GetLocalGroupCommand(CommandBase):
    cmd = "get-localgroup"
    needs_admin = False
    help_cmd = "get-localgroup [-server <servername] [-group <groupname>]"
    description = "Get localgroups on a host, or members of a group if a group is specified."
    version = 1
    author = "@checkymander"
    argument_class = GetLocalGroupArguments
    attackmapping = ["T1069", "T1069.001"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
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
