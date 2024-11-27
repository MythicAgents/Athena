from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

from ..athena_utils.mythicrpc_utilities import create_mythic_file
from ..athena_utils.bof_utilities import *
import json
import binascii
import cmd 
import struct
import os
import subprocess


class KListArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="purge",
                type=ParameterType.Boolean,
                description="Purge tickets",
                default_value=False,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
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



    

class KListCommand(CoffCommandBase):
    cmd = "klist"
    needs_admin = False
    help_cmd = "klist [-purge]"
    description = """Displays a list of currently cached Kerberos tickets, purges tickets if -purge is specified
    
    Credit: The Outflank team for the original BOF - https://github.com/outflanknl/C2-Tool-Collection"""
    version = 1
    script_only = True
    supported_ui_features = ["T1558.003"]
    author = "Cornelis de Plaa (@Cn33liz)"
    argument_class = KListArguments
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

        OfArgs = []
        action = taskData.args.get_arg("purge")

        encoded_args = ""
        if action:
            OfArgs.append(generateWString("purge"))
            encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()
            # Read the COFF file from the proper directory
        
        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"outflank_bofs/klist",f"klist.{arch}.o")
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
