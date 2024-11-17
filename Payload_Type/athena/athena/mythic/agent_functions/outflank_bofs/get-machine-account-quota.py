from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.bof_utilities import *
import json
import binascii
import cmd 
import struct
import os
import subprocess


class GetMachineAccountArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class GetMachineAccountCommand(CoffCommandBase):
    cmd = "get-machine-account-quota"
    needs_admin = False
    help_cmd = """
get-machine-account-quota
    
Credit: The Outflank team for the original BOF - https://github.com/outflanknl/C2-Tool-Collection
    """
    description = """Read the MachineAccountQuota value from the Active Directory domain."""
    version = 1
    script_only = True
    supported_ui_features = []
    author = "Cornelis de Plaa (@Cn33liz)"
    argument_class = GetMachineAccountArguments
    attackmapping = ["T1136.002"]
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


        bof_path = f"/Mythic/athena/mythic/agent_functions/outflank_bofs/add_machine_account/GetMachineAccountQuota.o"
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

        resp = await MythicRPC().execute("create_subtask_group", tasks=[
            {"command": "coff", "params": {"coffFile":file_resp.response["agent_file_id"], "functionName":"go","arguments": "", "timeout":"60"}},
            ], 
            subtask_group_name = "coff", parent_task_id=taskData.Task.ID)

        # We did it!
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
