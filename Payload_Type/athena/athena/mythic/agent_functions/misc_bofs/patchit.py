from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
from ..athena_utils.bof_utilities import *
import json

class PatchItArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.ChooseOne,
                description="Action to perform",
                choices=["check", "all", "amsi", "etw", "revertAll", "revertAmsi", "revertEtw"],
                default_value="check",
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
        else:
            self.args["action"].value = self.command_line

            
    
    async def parse_dictionary(self, dictionary):
        self.load_args_from_dictionary(dictionary)

class PatchItCommand(CoffCommandBase):
    cmd = "patchit"
    needs_admin = False
    help_cmd = """All-in-one to patch, check and revert AMSI and ETW for x64 process
Available Commands:" .
Check if AMSI & ETW are patched:      patchit check
Patch AMSI and ETW:                   patchit all
Patch AMSI (AmsiScanBuffer):          patchit amsi
Patch ETW (EtwEventWrite):            patchit etw
Revert patched AMSI & ETW:            patchit revertAll
Revert patched AMSI:                  patchit revertAmsi
Revert patched ETW:                   patchit revertEtw
Note: check command only compares first 4 lines of addresses of functions"""
    description = """All-in-one to patch, check and revert AMSI and ETW for x64 process"""
    version = 1
    script_only = True
    supported_ui_features = ["T1562.001"]
    author = "@ScriptIdiot"
    argument_class = PatchItArguments
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

        # Ensure architecture compatibility
        if taskData.Callback.Architecture != "x64":
            raise Exception("BOFs are currently only supported on x64 architectures.")

        # Define action mapping
        action_mapping = {
            "check": 1,
            "all": 2,
            "amsi": 3,
            "etw": 4,
            "revertall": 5,
            "revertamsi": 6,
            "revertetw": 7,
        }

        # Get and validate action
        action = taskData.args.get_arg("action").lower()
        if action not in action_mapping:
            raise Exception("Invalid action specified")

        # Prepare arguments
        encoded_args = base64.b64encode(
            SerializeArgs([generate32bitInt(action_mapping[action])])
        ).decode()

        # Compile and upload the BOF
        file_id = await compile_and_upload_bof_to_mythic(
            taskData.Task.ID,
            "misc_bofs/patchit",
            f"patchit.{taskData.Callback.Architecture}.o"
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

        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
