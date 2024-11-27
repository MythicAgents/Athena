from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

from Payload_Type.athena.athena.mythic.agent_functions.athena_utils.mythicrpc_utilities import create_mythic_file
from ..athena_utils.bof_utilities import *
import json
import binascii
import cmd 
import struct
import os
import subprocess
from ..athena_utils.bof_utilities import *


class GetPasswordPolicyArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="Hostname to enumerate the password policy of",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        )
                    ],
            ),]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
        else:
            raise ValueError("Missing arguments")
    
    async def parse_dictionary(self, dictionary):
        self.load_args_from_dictionary(dictionary)


class GetPasswordPolicyCommand(CoffCommandBase):
    cmd = "get-password-policy"
    needs_admin = False
    help_cmd = "get-password-policy"
    description = """Get target server or domain's configured password policy and lockouts
    get-password-policy -hostname 127.0.0.1
    get-password-policy 127.0.0.1
    
    Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Situational-Awareness-BOF/"""
    version = 1
    script_only = True
    supported_ui_features = ["T1087.002"]
    author = "@TrustedSec"
    argument_class = GetPasswordPolicyArguments
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

        encoded_args = ""
        OfArgs = []
        hostname = taskData.args.get_arg("hostname")
        if not hostname:
            hostname = "localhost"
            
        OfArgs.append(generateWString(hostname))
        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()

        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"trusted_sec_bofs/get_password_policy",f"get_password_policy.{arch}.o")
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
