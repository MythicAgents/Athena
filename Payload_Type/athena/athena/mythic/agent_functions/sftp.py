from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_utils import message_converter

class SftpArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
             CommandParameter(
                name="action",
                cli_name="action",
                display_name="Action",
                description="The Action to perform with the plugin",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Connect" # Many Args
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default" # Many Args
                    ),
                ],
            ),
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
                        group_name="Connect",
                        ui_position=1
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
                        group_name="Connect",
                        ui_position=2
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
                        group_name="Connect",
                        ui_position=3
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
                        group_name="Connect",
                        ui_position=4
                    )
                ],
            ),
            CommandParameter(
                name="args",
                cli_name="args",
                display_name="Args",
                description="Args to pass to the plugin",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=5
                    )
                ],   
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                temp_json = json.loads(self.command_line)
                if "host" in temp_json:
                    # this means we have tasking from the file browser rather than the popup UI
                    # the apfell agent doesn't currently have the ability to do _remote_ listings, so we ignore it
                    path = temp_json["path"] + "/" + temp_json["file"]
                    if(path == "//"):
                        self.add_arg("args", "/")
                    else:
                        self.add_arg("args", temp_json["path"] + "/" + temp_json["file"])
                    self.add_arg("action","ls")
                else:
                    self.load_args_from_json_string(self.command_line)
            else:
                args = self.command_line.split(" ")
                
                self.add_arg("action", args[0])
                if args[0] == "switch-session"  or args[0] == "disconnect":
                    self.add_arg("session", args[1])
                else:
                    self.add_arg("path" , args[1])   
        else:
            raise Exception("sftp requires at least one command-line parameter.\n\tUsage: {}".format(SftpCommand.help_cmd))

        pass


class SftpCommand(CommandBase):
    cmd = "sftp"
    needs_admin = False
    help_cmd = """
    Module Requirements: ssh

    Connect to SFTP host:
    sftp connect -hostname <host/ip> -username <user> [-password <password>] [-keypath </path/to/key>]
    
    Execute a command in the current session:
    sftp ls <path>

    Get current working path
    sftp pwd

    Set current working path
    sftp cd <path>

    Switch active session:
    sftp switch-session <session ID>
    
    List active sessions:
    sftp list-sessions

    Download a file:
    sftp download /full/path/to/file.txt

    Note: Downloaded files are not handled through the normal mythif file upload/download so they will need to be small enough to be read in the callback window.
    """
    description = "Interact with a given host using SFTP"
    version = 1
    supported_ui_features = ["file_browser:list"]
    author = "@checkymander"
    argument_class =SftpArguments
    attackmapping = ["T1071.002"]
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
