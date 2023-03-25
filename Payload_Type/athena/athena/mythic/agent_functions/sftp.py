from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_utils import message_converter

class SftpArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="action",
                cli_name="action",
                display_name="Action",
                description="The Action to perform with the plugin. [upload, download, connect, disconnect, list-sessions, switch-session, ls, cd, pwd]",
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
            CommandParameter(
                name="path",
                cli_name="path",
                display_name="Path",
                description="Path to list/change to",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    ),
                ],   
            ),
            CommandParameter(
                name="session",
                cli_name="session",
                display_name="Session",
                description="The session ID to switch to",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                    )
                ],   
            )
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
                        self.add_arg("path", "/")
                    else:
                        self.add_arg("path", temp_json["path"] + "/" + temp_json["file"])
                    self.add_arg("action","ls")
                else:
                    self.load_args_from_json_string(self.command_line)
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
    sftp switch-session -session <session ID>
    
    List active sessions:
    sftp list-sessions

    Download a file:
    sftp download /full/path/to/file.txt

    Note: Downloaded files are not handled through the normal mythif file upload/download so they will need to be small enough to be read in the callback window.
    """
    description = "Interact with a given host using SFTP"
    version = 1
    is_exit = False
    is_file_browse = True
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    supported_ui_features = ["file_browser:list"]
    author = "@checkymander"
    argument_class =SftpArguments
    #attackmapping = ["T1106", "T1083"]
    attackmapping = []
    attributes = CommandAttributes(
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
