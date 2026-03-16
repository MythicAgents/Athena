from ..athena_utils.plugin_utilities import default_ldap_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class AdwsQueryArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="filter", cli_name="filter",
                display_name="LDAP Filter",
                type=ParameterType.String,
                description="LDAP filter for the query",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
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

class AdwsQueryCommand(CommandBase):
    cmd = "adws-query"
    needs_admin = False
    script_only = True
    depends_on = "adws"
    plugin_libraries = []
    help_cmd = "adws-query -filter (objectClass=user) [-properties sAMAccountName,mail] [-searchbase DC=domain,DC=com]"
    description = "Query Active Directory via ADWS"
    version = 1
    author = "@checkymander"
    argument_class = AdwsQueryArguments
    attackmapping = ["T1087.002"]
    attributes = CommandAttributes(supported_os=[SupportedOS.Windows])
    completion_functions = {"command_callback": default_ldap_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="adws",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "query",
                "filter": taskData.args.get_arg("filter"),
                "properties": taskData.args.get_arg("properties"),
                "searchbase": taskData.args.get_arg("searchbase")
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
