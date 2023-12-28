from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

from .athena_utils import message_converter


class ShellArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="parent",
                type=ParameterType.String,
                description="If set, will spoof the parent process ID",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
            CommandParameter(
                name="commandline",
                type=ParameterType.String,
                description="The name of the process to inject into",
                parameter_group_info=[ParameterGroupInfo(ui_position=3)],
            ),
            CommandParameter(
                name="output",
                type=ParameterType.Boolean,
                description="Display assembly output. Default: True",
                parameter_group_info=[ParameterGroupInfo(ui_position=4)],
            ),
            CommandParameter(
                name="blockDlls",
                type=ParameterType.Boolean,
                description="If set, will only allow Microsoft signed DLLs to be loaded into the process. Default: False",
                parameter_group_info=[ParameterGroupInfo(ui_position=5)],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line.strip()) == 0:
            raise Exception("run requires a path to an executable to run.\n\tUsage: {}".format(ShellCommand.help_cmd))
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)   
        pass


class ShellCommand(CommandBase):
    cmd = "exec"
    needs_admin = False
    help_cmd = "exec [command] [arguments]"
    description = "Run a shell command which will translate to a process being spawned with command line: `cmd.exe /C [command]`"
    version = 1
    supported_ui_features = ["task_response:interactive"]
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = ShellArguments
    attackmapping = ["T1059", "T1059.004"]
    attributes = CommandAttributes(
        builtin=False
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