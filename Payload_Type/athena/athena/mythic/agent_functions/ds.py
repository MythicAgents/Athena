from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
import os
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
from os import listdir
from os.path import isfile, join

from .athena_utils import message_converter

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class DsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="action",
                cli_name="action",
                display_name="Action",
                type=ParameterType.String,
                description="Plugin subcommand",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0
                    ),
                ],
            ),
            CommandParameter(
                name="username",
                cli_name="username",
                display_name="User Name",
                type=ParameterType.String,
                description="Username to bind with",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                    )
                ],
            ),
            CommandParameter(
                name="password",
                type=ParameterType.String,
                cli_name="password",
                display_name="Password",
                description="Password to bind with",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                    ),
                ],
            ),
            CommandParameter(
                name="domain",
                cli_name="domain",
                display_name="Domain",
                type=ParameterType.String,
                description="The target domain",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                    ),
                ],
            ),            
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
                        ui_position=1
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
                        required=False,
                        group_name="Default",
                        ui_position=2
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
                    )
                ],
            ),
            CommandParameter(
                name="server",
                cli_name="server",
                display_name="Server",
                type=ParameterType.String,
                description="(Optional) The server to bind against",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                    ),
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
    ds connect [-server <server>] [-domain <domain>]

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
    browser_script = BrowserScript(script_name="ds", author="@checkymander")
    attackmapping = ["T1087.002", "T1069.002"]
    attributes = CommandAttributes(
    )

    # this function is called after all of your arguments have been parsed and validated that each "required" parameter has a non-None value
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


