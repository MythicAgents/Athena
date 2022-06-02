from mythic_payloadtype_container.MythicCommandBase import *
import json


class SftpArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="action",
                cli_name="action",
                display_name="The action to perform",
                description="Load a supported 3rd party library directly into the agent",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Upload"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Download"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Connect"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="ListSessions"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="SwitchSession"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="ListDirectory"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="ChangeDirectory"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default"
                    ),
                ],
            ),
            CommandParameter(
                name="hostname",
                cli_name="hostname",
                display_name="The connect host",
                description="Load a supported 3rd party library directly into the agent",
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
                name="username",
                cli_name="username",
                display_name="The username to login with",
                description="Load a supported 3rd party library directly into the agent",
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
                description="Key Path",
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
                display_name="path",
                description="Path to opperator on",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Upload",
                        ui_position=1
                    ),
                    ParameterGroupInfo(
                        required=True,
                        group_name="Download",
                        ui_position=1
                    ),
                    ParameterGroupInfo(
                        required=False,
                        group_name="ChangeDirectory",
                        ui_position=1
                    ),
                ],   
            ),
            CommandParameter(
                name="session",
                cli_name="session",
                display_name="session",
                description="Session to perform action on",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="ListSessions",
                        ui_position=1
                    )
                ],   
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
        else:
            raise Exception("ssh requires at least one command-line parameter.\n\tUsage: {}".format(SshCommand.help_cmd))

        pass


class SftpCommand(CommandBase):
    cmd = "sftp"
    needs_admin = False
    help_cmd = "sftp [/path/to/directory]"
    description = "Interact with an sftp server"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class =SftpArguments
    attackmapping = ["T1106", "T1083"]
    attributes = CommandAttributes(
        load_only=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
