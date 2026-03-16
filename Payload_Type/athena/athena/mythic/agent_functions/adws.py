from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class AdwsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["connect", "query", "disconnect"],
                default_value="connect",
                description="ADWS action to perform",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
            CommandParameter(
                name="server", cli_name="server",
                display_name="Server",
                type=ParameterType.String,
                default_value="",
                description="Domain controller hostname or IP",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
            CommandParameter(
                name="filter", cli_name="filter",
                display_name="LDAP Filter",
                type=ParameterType.String,
                default_value="",
                description="LDAP filter for query action",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
            CommandParameter(
                name="properties", cli_name="properties",
                display_name="Properties",
                type=ParameterType.String,
                default_value="",
                description="Comma-separated properties to return",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
            CommandParameter(
                name="searchbase", cli_name="searchbase",
                display_name="Search Base",
                type=ParameterType.String,
                default_value="",
                description="LDAP search base DN",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class AdwsCommand(CommandBase):
    cmd = "adws"
    needs_admin = False
    depends_on = None
    plugin_libraries = ["System.ServiceModel.NetTcp.dll"]
    help_cmd = "adws -action connect -server dc01.domain.com"
    description = "Active Directory Web Services (ADWS) enumeration over net.tcp:9389"
    version = 1
    author = "@checkymander"
    argument_class = AdwsArguments
    attackmapping = ["T1087.002"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows]
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
