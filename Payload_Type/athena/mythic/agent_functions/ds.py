from mythic_payloadtype_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
import os
# import the code for interacting with Files on the Mythic server
from mythic_payloadtype_container.MythicRPC import *
from os import listdir
from os.path import isfile, join

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class DsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="action",
                cli_name="action",
                display_name="Action",
                type=ParameterType.String,
                description="Plugin subcommand",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0
                    ),
                    ParameterGroupInfo(
                        required=True,
                        group_name="Connect",
                        ui_position=0
                    ),
                    ParameterGroupInfo(
                        required=True,
                        group_name="Query",
                        ui_position=0
                    ),
                ],
            ),
            CommandParameter(
                name="username",
                cli_name="usernmae",
                display_name="User Name",
                type=ParameterType.String,
                description="Username to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Connect",
                    )
                ],
            ),
            CommandParameter(
                name="password",
                type=ParameterType.String,
                cli_name="password",
                display_name="Password",
                description="Password to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Connect",
                    ),
                ],
            ),
            CommandParameter(
                name="domain",
                cli_name="domain",
                display_name="Domain",
                type=ParameterType.String,
                description="The target domain",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Connect",
                    ),
                    ParameterGroupInfo(
                        required=False,
                        group_name="Query",
                        ui_position=0
                    ),
                ],
            ),            
            CommandParameter(
                name="ldapfilter",
                cli_name="ldapfilter",
                display_name="Ldap Filter",
                type=ParameterType.String,
                description="(Optional) LdapFilter to query against",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Query",
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
                        group_name="Query",
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
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Query",
                    )
                ],
            ),
            CommandParameter(
                name="server",
                cli_name="server",
                display_name="Server",
                type=ParameterType.String,
                description="(Optional) The server to bind against",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Connect",
                    ),
                ],
            ),
            CommandParameter(
                name="properties",
                cli_name="properties",
                display_name="Properties",
                type=ParameterType.String,
                description="(Optional) Properties to return (comma separated or the word 'all')",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Query",
                    )
                ],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class DsCommand(CommandBase):
    cmd = "ds"
    needs_admin = False
    help_cmd = """
    Module Requirements: domain
    Initiate a bind using specified credentials
    ds connect [-username <user>] [-password <password>] [-domain <domain>] [-server <server>]

    Initiate a bind using current context
    ds connect [-server <server>] [-domain <domani>]

    Perform a query
    ds query <ldapfilter> <objectcategory> [-properties <all or comma separated list>] [-searchbase <searchbase>]
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
    argument_class = DsArguments
    attackmapping = []
    browser_script = None
    attributes = CommandAttributes(
        load_only=True,
    )

    # this function is called after all of your arguments have been parsed and validated that each "required" parameter has a non-None value
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass


