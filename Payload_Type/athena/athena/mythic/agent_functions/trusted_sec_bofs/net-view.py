from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import create_mythic_file
from ..athena_utils.bof_utilities import *
import json


class NetViewArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="domain",
                type=ParameterType.String,
                description="Optional. Specified domain otherwise uses current domain.",
                parameter_group_info=[
                    ParameterGroupInfo(
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
 

class NetViewCommand(CoffCommandBase):
    cmd = "net-view"
    needs_admin = False
    help_cmd = """
    Summary: lists local workstations and servers
    Usage: net-view [-domain myDomain]

    Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Situational-Awareness-BOF/
    """
    description = "lists local workstations and servers"
    version = 1
    script_only = True
    supported_ui_features = []
    author = "@TrustedSec"
    argument_class = NetViewArguments
    attackmapping = ["T1046"]
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

        encoded_args = ""
        OfArgs = []
        domain = taskData.args.get_arg("domain")

        if not domain:
            OfArgs.append(generateWString(""))
        else:
            OfArgs.append(generateWString(domain))

        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode() 
        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"trusted_sec_bofs/netview",f"netview.{arch}.o")  
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
