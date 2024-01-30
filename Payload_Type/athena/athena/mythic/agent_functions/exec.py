from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

from .athena_utils import message_converter


class ExecArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="commandline",
                type=ParameterType.String,
                description="The commandline to run",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0
                    )],
            ),
            CommandParameter(
                name="spoofedcommandline",
                type=ParameterType.String,
                default_value = "",
                description="If set, will only allow Microsoft signed DLLs to be loaded into the process. Default: False",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    )],
            ),
            CommandParameter(
                name="parent",
                type=ParameterType.Number,
                default_value = 0,
                description="If set, will spoof the parent process ID",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=2
                    )],
            ),
            CommandParameter(
                name="output",
                type=ParameterType.Boolean,
                default_value = False,
                description="Display assembly output. Default: True",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=3
                    )],
            ),
            CommandParameter(
                name="suspended",
                type=ParameterType.Boolean,
                default_value = False,
                description="Start process suspended to perform additional actions before execution. Default: False",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=4
                    )],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line.strip()) == 0:
            raise Exception("run requires a path to an executable to run.\n\tUsage: {}".format(ExecCommand.help_cmd))
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)   
        pass


class ExecCommand(CommandBase):
    cmd = "exec"
    needs_admin = False
    help_cmd = "exec -ppid=1234 -cmdline=\"net user checkymander\" -spoofedcmdline=\"net user notcheckymander\" -output=true -suspended=false"
    description = "Executes a command on the target machine"
    version = 1
    author = "@checkymander"
    argument_class = ExecArguments
    attackmapping = ["T1059", "T1059.004", "T1059.003", "T1564.010", "T1134.004"]
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