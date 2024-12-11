from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
import json

class DriverSigsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = []

    async def parse_arguments(self):
        pass

class DriverSigsCommand(CoffCommandBase):
    cmd = "driver-sigs"
    needs_admin = False
    help_cmd = "driver-sigs"
    description = """
Summary: checks drivers for known edr vendor names
Usage: driver-sigs

Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Situational-Awareness-BOF/
"""
    version = 1
    script_only = True
    supported_ui_features = []
    author = "@TrustedSec"
    argument_class = DriverSigsArguments
    attackmapping = ["T1652"]
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

        # Ensure architecture compatibility
        if taskData.Callback.Architecture != "x64":
            raise Exception("BOFs are currently only supported on x64 architectures.")

        file_id = await compile_and_upload_bof_to_mythic(
            taskData.Task.ID,
            "trusted_sec_bofs/driversigs",
            f"driversigs.{taskData.Callback.Architecture}.o"
        )

        subtask = await SendMythicRPCTaskCreateSubtask(MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID, 
            CommandName="coff",
            SubtaskCallbackFunction="coff_completion_callback",
            Params=json.dumps({
                "coffFile": file_id,
                "functionName": "go",
                "arguments": "",
                "timeout": "30",
            }),
            Token=taskData.Task.TokenID,
        ))
        
        # We did it!
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass