from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.bof_utilities import *
import json
import binascii
import cmd 
import struct
import os
import subprocess


class DeleteMachineAccountArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="name",
                type=ParameterType.String,
                description="Machine account to delete",
                default_value="",
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
    

class DeleteMachineAccountCommand(CoffCommandBase):
    cmd = "delete-machine-account"
    needs_admin = False
    help_cmd = """delete-machine-account [Computername]
    
Credit: The Outflank team for the original BOF - https://github.com/outflanknl/C2-Tool-Collection 
    """
    description = "Remove a computer account from the Active Directory domain."
    version = 1
    script_only = True
    supported_ui_features = ["T1136.002"]
    author = "Cornelis de Plaa (@Cn33liz)"
    argument_class = DeleteMachineAccountArguments
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


        bof_path = f"/Mythic/athena/mythic/agent_functions/outflank_bofs/add_machine_account/DelMachineAccount.o"
        if(os.path.isfile(bof_path) == False):
            await compile_bof("/Mythic/athena/mythic/agent_functions/outflank_bofs/add_machine_account/")

        # Read the COFF file from the proper directory
        with open(bof_path, "rb") as f:
            coff_file = f.read()

        # Upload the COFF file to Mythic, delete after using so that we don't have a bunch of wasted space used
        file_resp = await SendMythicRPCFileCreate(MythicRPCFileCreateMessage(
                taskData.Task.ID,
                DeleteAfterFetch = True,
                FileContents = coff_file,
            ))
        
        # Initialize our Argument list object
        OfArgs = []
        #Pack our argument and add it to the list
        computername = taskData.args.get_arg("name")
        #Repeat this for every argument being passed to the COFF (Changing the type as needed)
        OfArgs.append(generateWString(computername))
        # Serialize our arguments into a single buffer and base64 encode it
        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()

        # Delegate the execution to the coff command, passing: 
        #   the file_id from our create_file RPC call
        #   the functionName which in this case is go
        #   the number of arguments we packed which in this task is 1
        #   the arguments as a base64 encoded string generated by the OfArgs class
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