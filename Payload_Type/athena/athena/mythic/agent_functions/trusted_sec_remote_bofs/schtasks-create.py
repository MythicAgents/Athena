from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
import json

class SchTasksCreateArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="taskfile",
                type=ParameterType.File,
                description="Required. The file for the created task.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="taskpath",
                type=ParameterType.String,
                description="Required. The path for the created task.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="usermode",
                type=ParameterType.String,
                description="Required. The username to associate with the task. (user, xml, system)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="forcemode",
                type=ParameterType.String,
                description="Required. Creation disposition. (create, update)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=3,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="Optional. The system on which to create the task.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
                        required=False,
                        )
                    ],
            ),

        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
        else:
            raise ValueError("Missing arguments")
    
    async def parse_dictionary(self, dictionary):
        self.load_args_from_dictionary(dictionary)

class SchTasksCreateCommand(CoffCommandBase):
    cmd = "schtasks-create"
    needs_admin = False
    help_cmd = "schtasks-create"
    description = "Enumerate CAs and templates in the AD using Win32 functions (Created by TrustedSec)"
    version = 1
    script_only = True
    supported_ui_features = ["T1053.005"]
    author = "@TrustedSec"
    argument_class = SchTasksCreateArguments
    attackmapping = []
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )

        # Supported modes
        usermode = {"user": 0, "system": 1, "xml": 2}
        forcemode = {"create": 0, "update": 1}

        # Ensure architecture compatibility
        if taskData.Callback.Architecture != "x64":
            raise Exception("BOF's are currently only supported on x64 architectures.")

        # Validate and map modes
        str_mode = taskData.args.get_arg("usermode").lower()
        force_mode = taskData.args.get_arg("forcemode").lower()

        if str_mode not in usermode:
            raise Exception("Invalid usermode. Must be 'user', 'system', or 'xml'.")
        if force_mode not in forcemode:
            raise Exception("Invalid forcemode. Must be 'create' or 'update'.")

        mode = usermode[str_mode]
        force = forcemode[force_mode]

        # Prepare arguments
        file_contents = await get_mythic_file(taskData.args.get_arg("taskfile"))
        encoded_args = base64.b64encode(
            SerializeArgs([
            generateWString(taskData.args.get_arg("hostname")),
            generateWString(taskData.args.get_arg("taskpath")),
            generateWString(file_contents),
            generate32bitInt(mode),
            generate32bitInt(force),
            ])
        ).decode()

        # Compile and upload the BOF
        file_id = await compile_and_upload_bof_to_mythic(
            taskData.Task.ID,
            "trusted_sec_remote_bofs/schtaskscreate",
            f"schtaskscreate.{taskData.Callback.Architecture}.o"
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
