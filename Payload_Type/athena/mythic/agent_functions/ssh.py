from mythic_payloadtype_container.MythicCommandBase import *
import json
import sys


class SshArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
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
                name="args",
                cli_name="args",
                display_name="Args",
                description="Args to pass to the plugin",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position = 1,
                        required=False,
                        group_name="Default"
                    )
                ],   
            ),
            # CommandParameter(
            #     name="session",
            #     cli_name="session",
            #     display_name="Session",
            #     description="The session ID to switch to",
            #     type=ParameterType.String,
            #     default_value = "",
            #     parameter_group_info=[
            #         ParameterGroupInfo(
            #             required=False,
            #             group_name="Default",
            #         )
            #     ],   
            # )
        ]

    async def parse_arguments(self):        
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                temp_json = json.loads(self.command_line)
                if temp_json["action"]=="switch":
                    if "session" not in temp_json:
                        self.set_arg("session", temp_json["command"])
                    self.set_arg("action", "switch")
                elif temp_json["action"]=="exec":
                    self.set_arg("action", "exec")
                    self.set_arg("command", temp_json["command"])
                    self.set_arg("command_line", self.command_line)
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
    ssh exec "<command to exec>"

    Switch active session:
    ssh switch -session <session ID>
    
    List active sessions:
    ssh list
    """
    description = "Interact with a given host using SSH"
    version = 1
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
        load_only=False,
        builtin=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:  
        return task

    async def process_response(self, response: AgentResponse):
        pass
