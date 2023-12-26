from mythic_container.MythicCommandBase import *
import json
import sys
from mythic_container.MythicRPC import *

from .athena_utils import message_converter

class SshArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="hostname",
                cli_name="hostname",
                display_name="Host Name",
                description="The IP or Hostname to connect to",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Connect"
                    )
                ],
            ),
            CommandParameter(
                name="username",
                cli_name="username",
                display_name="Username",
                description="The username to authenticate with",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Connect"
                    )
                ],
            ),
            CommandParameter(
                name="password",
                cli_name="password",
                display_name="Password",
                description="The user password/key passphrase",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Connect"
                    )
                ],
            ),
            CommandParameter(
                name="keypath",
                cli_name="keypath",
                display_name="Key Path",
                description="Path to an SSH key to use for authentication",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Connect"
                    )
                ],
            ),
        ]

    async def parse_arguments(self):        
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                temp_json = json.loads(self.command_line)
                if temp_json["action"]=="switch":
                    if "session" not in temp_json:
                        self.set_arg("session", temp_json["args"])
                    self.set_arg("action", "switch")
                elif temp_json["action"]=="exec":
                    self.set_arg("action", "exec")
                    self.set_arg("command", temp_json["args"])
                    #self.set_arg("command_line", self.command_line)
                else:
                    self.load_args_from_json_string(self.command_line)
            else:
                if self.command_line.split(" ")[0] == "exec":
                    self.add_arg("command", self.command_line.split(" ",1)[1].strip())       
        else:
            raise Exception("ssh requires at least one command-line parameter.\n\tUsage: {}".format(SshCommand.help_cmd))

        #self.add_arg("test",self.command_line)

        pass


class SshCommand(CommandBase):
    cmd = "ssh"
    needs_admin = False
    help_cmd = """
    Module Requirements: ssh

    Connect to SSH host:
    ssh connect -hostname <host/ip> -username <user> [-password <password>] [-keypath </path/to/key>]
    
    Execute a command in the current session:
    ssh exec <command to exec>

    Switch active session:
    ssh switch-session <session ID>
    
    List active sessions:
    ssh list-sessions
    """
    description = "Interact with a given host using SSH"
    version = 1
    supported_ui_features = ["task_response:interactive"]
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = SshArguments
    attackmapping = ["T1059", "T1059.004"]
    attributes = CommandAttributes(
        load_only=True
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
