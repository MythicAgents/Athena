from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json
import binascii
import cmd 
import struct
import os
import subprocess
from ..athena_utils.bof_utilities import *


class SchTasksRunArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="taskname",
                type=ParameterType.String,
                description="The scheduled task name to start.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="Optional. The target system (local system if not specified)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
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
   

class SchTasksRunCommand(CoffCommandBase):
    cmd = "schtasks-run"
    needs_admin = False
    help_cmd = """
Summary: This command runs a scheduled task.
Usage:   schtasks-run -hostname GAIA-DC -taskname \\Microsoft\\Windows\\MUI\\LpRemove
         hostname  Optional. The target system (local system if not specified)
         taskname  Required. The scheduled task name.
Note:    The full path including the task name must be given, e.g.:
             schtasks-run \\Microsoft\\Windows\\MUI\\LpRemove
             schtasks-run \\Microsoft\\windows\\MUI\\totallyreal

Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Remote-OPs-BOF
    """
    description = "This command runs a scheduled task."
    version = 1
    script_only = True
    supported_ui_features = ["T1053.005"]
    author = "@TrustedSec"
    argument_class = SchTasksRunArguments
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


        bof_path = f"/Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/schtasksrun/schtasksrun.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await compile_bof("/Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/schtasksrun/")

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
        hostname = taskData.args.get_arg("hostname")
        if hostname:
            OfArgs.append(generateWString(hostname))
        else:
            OfArgs.append(generateWString(""))
        taskname = taskData.args.get_arg("taskname")
        OfArgs.append(generateWString(taskname))
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

    async def process_response(self, response: AgentResponse):
        response.response["output"] = response.response["output"].replace("\\r\\n", "\r\n")
        pass
