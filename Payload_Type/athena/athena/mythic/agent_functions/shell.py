from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

from .athena_utils import message_converter


class ShellArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="executable",
                cli_name="Executable",
                display_name="Executable",
                type=ParameterType.String,
                description="Path to an executable to run.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default" # Many Args
                    ),
                ],
            ),
            CommandParameter(
                name="arguments",
                cli_name="Arguments",
                display_name="Arguments",
                type=ParameterType.String,
                default_value="",
                description="Arguments to pass to the executable.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        ui_position=1,
                        group_name="Default" # Many Args
                    ),
                ]),
        ]

    async def parse_arguments(self):
        if len(self.command_line.strip()) == 0:
            raise Exception("run requires a path to an executable to run.\n\tUsage: {}".format(ShellCommand.help_cmd))
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            self.load
            parts = self.command_line.split(" ", 1)
            self.add_arg("executable", parts[0])
            if len(parts) > 1:
                self.add_arg("arguments", parts[1])
        pass


class ShellCommand(CommandBase):
    cmd = "shell"
    needs_admin = False
    help_cmd = "shell [command] [arguments]"
    description = "Run a shell command which will translate to a process being spawned with command line: `cmd.exe /C [command]`"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = ShellArguments
    #attackmapping = ["T1059", "T1059.004"]
    attackmapping = []
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