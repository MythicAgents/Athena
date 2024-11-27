
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json
import os

from ..athena_utils.mythicrpc_utilities import create_mythic_file
from ..athena_utils.bof_utilities import *


class AddUserToGroupArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="username",
                type=ParameterType.String,
                description="Required. The user name to add to the group.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        default_value=""
                        )
                    ],
            ),
            CommandParameter(
                name="groupname",
                type=ParameterType.String,
                description="Required. The group to add the user to.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="Required. The target computer to perform the addition on.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=False,
                        )
                    ],
            ),
            CommandParameter(
                name="domain",
                type=ParameterType.String,
                description="The domain/computer for the account. You must give the domain name for the user if it is a domain account.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=3,
                        required=False,
                        )
                    ],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
        else:
            raise ValueError("Missing arguments")
    
    async def parse_dictionary(self, dictionary):
        self.load_args_from_dictionary(dictionary)

class AddUserToGroupCommand(CoffCommandBase):
    cmd = "add-user-to-group"
    needs_admin = False
    help_cmd = """
    Summary: Add the specified user to the group. Domain groups only!

Usage:   add-user-to-group -username checkymander -groupname "Domain Admins" [-hostname GAIA-DC] [-domain METEOR]
         username   Required. The user name to activate/enable. 
         groupname  Required. The group to add the user to.
         hostname   Optional. The target computer to perform the addition on.
         domain     Optional. The domain/computer for the account. You must give 
                    the domain name for the user if it is a domain account.
                    
Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Remote-OPs-BOF"""
    description = """Add the specified user to the group. Domain groups only!"""
    version = 1
    script_only = True
    supported_ui_features = []
    author = "@TrustedSec"
    argument_class = AddUserToGroupArguments
    attackmapping = ["T1098"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
        builtin=False,
        load_only=True
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )

        arch = taskData.Callback.Architecture

        if(arch=="x86"):
            raise Exception("BOF's are currently only supported on x64 architectures")

        # Domain - 5
        # Server - 4
        # Username - 2
        # Groupname - 3
        encoded_args = ""
        OfArgs = []
        
        domain = taskData.args.get_arg("domain")
        if not domain:
            OfArgs.append(generateWString(""))
        else:
            OfArgs.append(generateWString(domain))
        
        hostname = taskData.args.get_arg("hostname")    
        if not hostname:
            OfArgs.append(generateWString(""))
        else:    
            OfArgs.append(generateWString(hostname))
        
        username = taskData.args.get_arg("username")
        OfArgs.append(generateWString(username))
        groupname = taskData.args.get_arg("groupname")
        OfArgs.append(generateWString(groupname))

        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()

        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"trusted_sec_remote_bofs/addusertogroup",f"addusertogroup.{arch}.o")
        subtask = await SendMythicRPCTaskCreateSubtask(MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID, 
            CommandName="coff",
            SubtaskCallbackFunction="coff_completion_callback",
            Params=json.dumps({
                "coffFile": file_id,
                "functionName": "go",
                "arguments": encoded_args,
                "timeout": "60",
            }),
            Token=taskData.Task.TokenID,
        ))

        # We did it!
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass