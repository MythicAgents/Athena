from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
import os
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
from os import listdir
from os.path import isfile, join

from .athena_utils import message_converter

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class DsConnectArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
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
                        ui_position=0,
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
                        ui_position=1,
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
                        ui_position=2,
                    ),
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
                        ui_position=3,
                    ),
                ],
            ),           
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class DsConnectCommand(CommandBase):
    cmd = "ds-connect"
    needs_admin = False
    script_only = True
    help_cmd = """
    Initiate a bind using specified credentials
    ds-connect [-username <user>] [-password <password>] [-domain <domain>] [-server <server>]

    Initiate a bind using current context
    ds-connect [-server <server>] [-domain <domain>]
    """
    description = "Bind to an LDAP Controller"
    version = 1
    author = "@checkymander"
    argument_class = DsConnectArguments
    attackmapping = []
    attributes = CommandAttributes(
    )

    # this function is called after all of your arguments have been parsed and validated that each "required" parameter has a non-None value
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage(taskData.Task.ID, 
                                                                 CommandName="ds", 
                                                                 Token=taskData.Task.TokenID,
                                                                 Params=json.dumps(
                                                                    {"action": "connect", 
                                                                     "username": taskData.args.get_arg("username"),
                                                                     "password": taskData.args.get_arg("password"),
                                                                     "domain": taskData.args.get_arg("domain"),
                                                                     "server": taskData.args.get_arg("server"),})
                                                                     )
        subtask = await SendMythicRPCTaskCreateSubtask(createSubtaskMessage)


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


