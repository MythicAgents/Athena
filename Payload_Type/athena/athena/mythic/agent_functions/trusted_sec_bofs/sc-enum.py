from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
import json

class ScEnumArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="Hostname to enumerate the services of",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        )
                    ],
            ),]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
        else:
            raise ValueError("Missing arguments")
    
    async def parse_dictionary(self, dictionary):
        self.load_args_from_dictionary(dictionary)



    

class ScEnumCommand(CoffCommandBase):
    cmd = "sc-enum"
    needs_admin = False
    help_cmd = """
    Summary: Enumerate all service configs in depth
    Usage: sc-enum [-hostname myHost]
    
    Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Situational-Awareness-BOF/
    """
    description = "Enumerate all service configs in depth"
    version = 1
    script_only = True
    supported_ui_features = ["T1543.003"]
    author = "@TrustedSec"
    argument_class = ScEnumArguments
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

        # Create our BeaconPack object to handle the Argument packing
        encoded_args = ""
        OfArgs = []  
        hostname = taskData.args.get_arg("hostname")
        if hostname:
            OfArgs.append(generateString(hostname))
        else:
            OfArgs.append(generateString(""))

        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()

        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"trusted_sec_bofs/sc_enum",f"sc_enum.{arch}.o")
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