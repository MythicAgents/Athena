from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_utils import message_converter

class PyLoadArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="file",
                type=ParameterType.File,
                description="Zip file containing the libraries to import",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


class PyLoadCommand(CommandBase):
    cmd = "python-load"
    needs_admin = False
    help_cmd = "python"
    description = "Load required libraries into python interpreter"
    version = 1
    author = "@checkymander"
    argument_class = PyLoadArguments
    #attackmapping = ["T1005", "T1552.001"]
    attackmapping = []
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
