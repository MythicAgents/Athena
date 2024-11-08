from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
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
        file_data = await SendMythicRPCFileSearch(MythicRPCFileSearchMessage(AgentFileID=taskData.args.get_arg("pyfile")))
        file = await SendMythicRPCFileGetContent(MythicRPCFileGetContentMessage(taskData.args.get_arg("pyfile")))
            
        if file.Success:
            file_contents = base64.b64encode(file.Content)
            taskData.args.add_arg("file", file_contents.decode("utf-8"), parameter_group_info=[ParameterGroupInfo(
                required=True,
            )])
        else:
            raise Exception("Failed to fetch uploaded file from Mythic (ID: {})".format(taskData.args.get_arg("pyfile")))
        original_file_name = file_data.Files[0].Filename
        response.DisplayParams = "python-exec {} {}".format(original_file_name, taskData.args.get_arg("args"))
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp