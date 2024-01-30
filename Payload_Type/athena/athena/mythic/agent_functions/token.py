from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

from .athena_utils import message_converter


class TokenArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [            
            CommandParameter(
                name="action",
                cli_name="action",
                display_name="Action",
                description="The domain to log on to (set to . for local accounts)",
                type=ParameterType.ChooseOne,
                choices = ["make", "steal", "list"],
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position = 0,
                    ),
                    ParameterGroupInfo(
                        required=True,
                        group_name="Steal",
                        ui_position = 0,
                    )
                ],
            ),
            CommandParameter(
                name="domain",
                cli_name="domain",
                display_name="Domain",
                description="The domain to log on to (set to . for local accounts)",
                type=ParameterType.String,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position = 1,
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
                        group_name="Default",
                        ui_position = 2,
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
                        group_name="Default",
                        ui_position = 3,
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
                        group_name="Default",
                        ui_position = 4,
                    )
                ],
            ),
            CommandParameter(
                name="pid",
                cli_name="pid",
                display_name="pid",
                description="The pid of the process to impersonate",
                type=ParameterType.Number,
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Steal",
                        ui_position = 1 
                    ),
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
                        group_name="Default",
                        ui_position = 5
                    ),
                    ParameterGroupInfo(
                        required=False,
                        group_name="Steal",
                        ui_position = 2
                    )
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
    token -action make -username <user> -password <password> -domain <domain> -netonly true -name <descriptive name>
    token -action make -username myuser@contoso.com -password P@ssw0rd -netonly true
    token -action make -username myuser -password P@ssword -domain contoso.com -netonly false
    
    Create a new token for a local user:
    token -action make -username mylocaladmin -password P@ssw0rd! -domain . -netonly true
    """
    description = "Change impersonation context for current user"
    version = 1
    supported_ui_features = []
    author = "@checkymander"
    argument_class = TokenArguments
    attackmapping = ["T1134.001", "T1134.003"]
    attributes = CommandAttributes(
        builtin=False,
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