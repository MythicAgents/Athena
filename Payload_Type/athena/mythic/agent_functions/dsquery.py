from mythic_payloadtype_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
import os
# import the code for interacting with Files on the Mythic server
from mythic_payloadtype_container.MythicRPC import *
from os import listdir
from os.path import isfile, join

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class DsqueryArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="username",
                type=ParameterType.String,
                description="Username to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="password",
                type=ParameterType.String,
                description="Password to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="domain",
                type=ParameterType.String,
                description="Domain to bind against",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),            
            CommandParameter(
                name="ldapfilter",
                type=ParameterType.String,
                description="(Optional) LdapFilter to query against",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="objectcategory",
                type=ParameterType.ChooseOne,
                choices=[
                    "user",
                    "group",
                    "ou",
                    "computer",
                    "*",
                ],
                description="Object to query against",
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
                type=ParameterType.String,
                description="(Optional) The searchbase to perform the query against",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="server",
                type=ParameterType.String,
                description="(Optional) The server to bind against",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="properties",
                type=ParameterType.String,
                description="(Optional) Properties to return (comma separated or the word 'all')",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
        ]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class DsqueryCommand(CommandBase):
    cmd = "dsquery"
    needs_admin = False
    help_cmd = """
    Module Requirements: domain

    dsquery -username <user> -password <pass> -domain contoso.local -ldapfilter "(serviceprincipalname=*)" -properties all 
    dsquery -username <user> -password <pass> -domain contoso.local -ldapfilter "(serviceprincipalname=*)" -properties samaccountname,description,serviceprincipalname
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
    argument_class = DsqueryArguments
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


