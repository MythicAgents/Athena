from mythic_payloadtype_container.MythicCommandBase import *
import json


class SshArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="action",
                cli_name="action",
                display_name="The username to login with",
                description="Load a supported 3rd party library directly into the agent",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default"
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
                        required=True,
                        ui_position=1,
                        group_name="Connect"
                    )
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
                        required=True,
                        ui_position=2,
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
                        ui_position=3,
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
                        ui_position=4,
                        group_name="Connect"
                    )
                ],
            ),
            CommandParameter(
                name="parameters",
                cli_name="parameters",
                display_name="parameters",
                description="Command to exec",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default"
                    )
                ],
            )]
        

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                parts = self.command_line.split()
                if(parts[0].lower() == "exec"):
                    command_line = " ".join(str(part) for part in range(1,len(parts)))
                    task.args.add_arg("command", command_line)

        else:
            raise Exception("ssh requires at least one command-line parameter.\n\tUsage: {}".format(SshCommand.help_cmd))

        pass


class SshCommand(CommandBase):
    cmd = "ssh"
    needs_admin = False
    help_cmd = "ssh [action] [arguments]"
    description = "Run an ssh command agains ta specific host`"
    version = 2
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
