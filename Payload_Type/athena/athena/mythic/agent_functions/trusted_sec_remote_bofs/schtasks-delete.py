from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json
import binascii
import cmd 
import struct
import os
import subprocess
from ..athena_utils.bof_utilities import *


class SchTasksDeleteArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="taskname",
                type=ParameterType.String,
                description="Required. The task or folder name.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="tasktype",
                type=ParameterType.String,
                description="Required. The type of target to delete. [folder task]",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=False,
                        )
                    ],
            ),
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="Optional. The target system (local system if not specified)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=False,
                        )
                    ],
            )
        ]

    async def parse_arguments(self):
        pass


class SchTasksDeleteCommand(CoffCommandBase):
    cmd = "schtasks-delete"
    needs_admin = False
    help_cmd = "schtasks-delete"
    description = "Enumerate CAs and templates in the AD using Win32 functions (Created by TrustedSec)"
    version = 1
    script_only = True
    supported_ui_features = ["T1053.005"]
    author = "@TrustedSec"
    argument_class = SchTasksDeleteArguments
    attackmapping = []
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )

        arch = taskData.Callback.Architecture


        if(arch=="x86"):
            raise Exception("BOF's are currently only supported on x64 architectures")


        bof_path = f"Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/schtasksdelete/schtasksdelete.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await compile_bof("Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/schtasksdelete/")

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
        OfArgs.append(generateWString(hostname))
        taskname = taskData.args.get_arg("taskname")
        OfArgs.append(generateWString(taskname))

        task_type = taskData.args.get_arg("tasktype")

        if(task_type == "folder"):
            OfArgs.append(generate32bitInt(1))
        else:
            OfArgs.append(generate32bitInt(0))


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
