from .athena_utils.plugin_utilities import default_ldap_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class DsQueryArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [         
            CommandParameter(
                name="ldapfilter",
                cli_name="ldapfilter",
                display_name="Ldap Filter",
                type=ParameterType.String,
                description="(Optional) LdapFilter to query against",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=0,
                    )
                ],
            ),
            CommandParameter(
                name="objectcategory",
                cli_name="objectcategory",
                display_name="Object Category",
                type=ParameterType.ChooseOne,
                choices=[
                    "user",
                    "group",
                    "ou",
                    "computer",
                    "*",
                ],
                description="Object to query against",
                default_value="*",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="searchbase",
                cli_name="searchbase",
                display_name="Search Base",
                type=ParameterType.String,
                description="(Optional) The searchbase to perform the query against",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=2,
                    )
                ],
            ),
            CommandParameter(
                name="properties",
                cli_name="properties",
                display_name="Properties",
                type=ParameterType.String,
                description="(Optional) Properties to return (comma separated or the word 'all')",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=3,
                    )
                ],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class DsQueryCommand(CommandBase):
    cmd = "ds-query"
    needs_admin = False
    script_only = True
    help_cmd = """
    ds-query <ldapfilter> <objectcategory> [-properties <all or comma separated list>] [-searchbase <searchbase>]
    """
    description = "Run an LDAP Query against a Domain Controller"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@checkymander"
    argument_class = DsQueryArguments
    attackmapping = ["T1087.002","T1069.002"]
    attributes = CommandAttributes(
    )
    completion_functions = {"command_callback": default_ldap_completion_callback}

    # this function is called after all of your arguments have been parsed and validated that each "required" parameter has a non-None value
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage(taskData.Task.ID, 
                                                                CommandName="ds", 
                                                                Token=taskData.Task.TokenID,
                                                                SubtaskCallbackFunction="command_callback",
                                                                Params=json.dumps(
                                                                {"action": "query", 
                                                                    "objectcategory": taskData.args.get_arg("objectcategory"),
                                                                    "ldapfilter": taskData.args.get_arg("ldapfilter"),
                                                                    "searchbase": taskData.args.get_arg("searchbase"),
                                                                    "properties": taskData.args.get_arg("properties")}))
        subtask = await SendMythicRPCTaskCreateSubtask(createSubtaskMessage)
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass


