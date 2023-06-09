from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

from .athena_utils import message_converter


class TokenArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="domain",
                cli_name="domain",
                display_name="Domain",
                description="The domain to log on to (set to . for local accounts)",
                type=ParameterType.String,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default"
                    )
                ],
            ),
            CommandParameter(
                name="username",
                cli_name="username",
                display_name="Username",
                description="The username to impersonate",
                type=ParameterType.String,
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default"
                    )
                ],
            ),
            CommandParameter(
                name="password",
                cli_name="password",
                display_name="Password",
                description="The user password",
                type=ParameterType.String,
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default"
                    )
                ],
            ),
            CommandParameter(
                name="netonly",
                cli_name="netonly",
                display_name="NetOnly",
                description="Perform a netonly logon",
                type=ParameterType.Boolean,
                default_value=False,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default"
                    )
                ],
            ),
            CommandParameter(
                name="name",
                cli_name="name",
                display_name="Name",
                description="A descriptive name for the token",
                type=ParameterType.String,
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default"
                    ),
                ],
            ),
        ]
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.load_args_from_cli_string(self.command_line)

class TokenCommand(CommandBase):
    cmd = "token"
    needs_admin = False
    help_cmd = """
    Create a new token for a domain user:
    token -username <user> -password <password> -domain <domain> -netonly true -name <descriptive name>
    token -username myuser@contoso.com -password P@ssw0rd -netonly true
    token -username myuser -password P@ssword -domain contoso.com -netonly false
    
    Create a new token for a local user:
    token -username mylocaladmin -password P@ssw0rd! -domain . -netonly true
    """
    description = "Change impersonation context for current user"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    supported_ui_features = []
    author = "@checkymander"
    argument_class = TokenArguments
    attackmapping = []
    attributes = CommandAttributes(
        builtin=True,
        supported_os=[SupportedOS.Windows],
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