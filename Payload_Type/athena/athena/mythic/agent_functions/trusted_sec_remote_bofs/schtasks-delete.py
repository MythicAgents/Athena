from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
import json


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

        # Ensure architecture compatibility
        if taskData.Callback.Architecture != "x64":
            raise Exception("BOF's are currently only supported on x64 architectures.")

        # Prepare arguments
        encoded_args = base64.b64encode(
            SerializeArgs([
            generateWString(taskData.args.get_arg("hostname")),
            generateWString(taskData.args.get_arg("taskname")),
            generate32bitInt(1 if taskData.args.get_arg("tasktype") == "folder" else 0),
            ])
        ).decode()

        # Compile and upload the BOF
        file_id = await compile_and_upload_bof_to_mythic(
            taskData.Task.ID,
            "trusted_sec_remote_bofs/schtasksdelete",
            f"schtasksdelete.{taskData.Callback.Architecture}.o"
        )

        # Create the subtask
        subtask = await SendMythicRPCTaskCreateSubtask(
            MythicRPCTaskCreateSubtaskMessage(
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
            )
        )

        # Return the response
        return response


    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
