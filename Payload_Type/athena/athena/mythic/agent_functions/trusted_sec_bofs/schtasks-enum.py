from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.bof_utilities import *
import json
import binascii
import cmd 
import struct
import os
import subprocess


class SchtasksEnumArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="Hostname to enumerate the scheduled tasks of",
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

class SchtasksEnumCommand(CoffCommandBase):
    cmd = "schtasks-enum"
    needs_admin = False
    help_cmd = """
Summary: enumerates all scheduled tasks on the local or target machine
Usage: schtasks-enum [-hostname MyHost]

Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Situational-Awareness-BOF/
"""
    description = "enumerates all scheduled tasks on the local or target machine"
    version = 1
    script_only = True
    supported_ui_features = ["T1053.005"]
    author = "@TrustedSec"
    argument_class = SchtasksEnumArguments
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


        bof_path = f"/Mythic/athena/mythic/agent_functions/trusted_sec_bofs/schtasksenum/schtasksenum.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await compile_bof("/Mythic/athena/mythic/agent_functions/trusted_sec_bofs/schtasksenum/")

        # Read the COFF file from the proper directory
        with open(bof_path, "rb") as f:
            coff_file = f.read()

        # Upload the COFF file to Mythic, delete after using so that we don't have a bunch of wasted space used
        file_resp = await SendMythicRPCFileCreate(MythicRPCFileCreateMessage(
                taskData.Task.ID,
                DeleteAfterFetch = True,
                FileContents = coff_file,
            ))
        
        # Create our BeaconPack object to handle the Argument packing
        encoded_args = ""
        OfArgs = []
        hostname = taskData.args.get_arg("hostname")

        if hostname:
            OfArgs.append(generateWString(hostname))
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
