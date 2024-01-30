from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class KillArguments(TaskArguments):

    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="id",
                cli_name="id",
                display_name="id",
                type=ParameterType.Number,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="ID",
                        ui_position=0,
                    ),
                ]
            ),
            CommandParameter(
                name="name",
                cli_name="name",
                display_name="name",
                type=ParameterType.String,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Name",
                        ui_position=0,
                    ),
                ]
            ),
            CommandParameter(
                name="tree",
                cli_name="tree",
                display_name="Tree",
                type=ParameterType.Boolean,
                description="Include child processes in the kill",
                default_value=False,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="ID",
                        ui_position=1,
                    ),                    
                    ParameterGroupInfo(
                        required=False,
                        group_name="Name",
                        ui_position=1,
                    )
                ],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) == 0:
            raise Exception("No PID given.")
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            try:
                int(self.command_line)
            except:
                self.add_arg("name", self.command_line, ParameterType.String)
            self.add_arg("pid", int(self.command_line), ParameterType.Number)
        

class killCommand(CommandBase):
    cmd = "kill"
    needs_admin = False
    help_cmd = "kill [id] [-tree True/False]"
    description = "Kill a process specified by an ID"
    version = 1
    author = "@checkymander"
    argument_class = KillArguments
    attackmapping = ["T1106"]
    supported_ui_features = ["kill"]
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