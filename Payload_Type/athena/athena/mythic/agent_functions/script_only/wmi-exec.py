from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json


class WmiExecArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="host", cli_name="host", display_name="Host",
                type=ParameterType.String,
                description="Target host",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="command", cli_name="command", display_name="Command",
                type=ParameterType.String,
                description="Command to execute",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="username", cli_name="username", display_name="Username",
                type=ParameterType.String, default_value="",
                description="Optional credentials username",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
            CommandParameter(
                name="password", cli_name="password", display_name="Password",
                type=ParameterType.String, default_value="",
                description="Optional credentials password",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=3)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


class WmiExecCommand(CommandBase):
    cmd = "wmi-exec"
    needs_admin = False
    script_only = True
    depends_on = "wmi"
    plugin_libraries = []
    help_cmd = "wmi-exec -host DC01 -command \"cmd.exe /c whoami\""
    description = "Execute a process on a remote host via WMI"
    version = 1
    author = "@checkymander"
    argument_class = WmiExecArguments
    attackmapping = ["T1047"]
    attributes = CommandAttributes(supported_os=[SupportedOS.Windows])
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID, CommandName="wmi",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "wmi-exec",
                "host": taskData.args.get_arg("host"),
                "command": taskData.args.get_arg("command"),
                "username": taskData.args.get_arg("username") or "",
                "password": taskData.args.get_arg("password") or "",
            }))
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
