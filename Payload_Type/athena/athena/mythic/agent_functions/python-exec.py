from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_utils.mythicrpc_utilities import *
from .athena_utils import message_converter

class PyExecArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="pyfile",
                type=ParameterType.File,
                description="Python File to execute",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
            CommandParameter(
                name="args",
                type=ParameterType.String,
                description="Args to pass to the script via argv",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
                default_value="",
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


class PyExecCommand(CommandBase):
    cmd = "python-exec"
    needs_admin = False
    help_cmd = "python"
    description = "Execute a python file using IronPython3 use python-load to add required dependencies (including the standard library)"
    version = 1
    author = "@checkymander"
    argument_class = PyExecArguments
    #attackmapping = ["T1005", "T1552.001"]
    attackmapping = []
    attributes = CommandAttributes(
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        encoded_file_contents = await get_mythic_file(taskData.args.get_arg("pyfile"))
        original_file_name = await get_mythic_file_name(taskData.args.get_arg("pyfile"))
        taskData.args.add_arg("file", encoded_file_contents, parameter_group_info=[ParameterGroupInfo(
                required=True,
            )])
        
        response.DisplayParams = "{} {}".format(original_file_name, taskData.args.get_arg("args"))
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp