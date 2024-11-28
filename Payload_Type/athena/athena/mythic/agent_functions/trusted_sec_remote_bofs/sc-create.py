from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
import json


class ScCreateArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="servicename",
                type=ParameterType.String,
                description="Required. The name of the service to create.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="displayname",
                type=ParameterType.String,
                description="Required. The display name of the service.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="binpath",
                type=ParameterType.String,
                description="Required. The binary path of the service to execute.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="description",
                type=ParameterType.String,
                description="Required. The description of the service.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=3,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="errormode",
                type=ParameterType.Number,
                description="Required. The error mode of the service. (0 = ignore errors, 1 = normal errors, 2 = severe errors, 3 = critical errors)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="startmode",
                type=ParameterType.Number,
                description="Required. The start mode for the service. (2 = auto start, 3 = demand start, 4 = disabled)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=5,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="Optional. The target system (local system if not specified)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=6,
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

class ScCreateCommand(CoffCommandBase):
    cmd = "sc-create"
    needs_admin = False
    help_cmd = """
Summary: This command creates a service on the target host.
Usage:   sc-create -servicename myService -displayname "Run the Jewels" -description "runnit fast" -binpath C:\\Users\\checkymander\\Desktop\\malware.exe -errormode 0 -startmode 2 -hostname GAIA-DC
         servicename      Required. The name of the service to create.
         displayname  Required. The display name of the service.
         binpath      Required. The binary path of the service to execute.
         description  Required. The description of the service.
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
    description = """This command creates a service on the target host."""
    version = 1
    script_only = True
    supported_ui_features = ["T1543.003"]
    author = "@TrustedSec"
    argument_class = ScCreateArguments
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

        #hostname (string) (Optional)
        #servicename (string)
        #binpath (string)
        #displayname (string)
        #desc (string)
        #errormode (int)
        #startmode (int)

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

        displayname = taskData.args.get_arg("displayname")
        OfArgs.append(generateString(displayname))

        description = taskData.args.get_arg("description")
        OfArgs.append(generateString(description))

        errormode = taskData.args.get_arg("errormode")
        OfArgs.append(generate16bitInt(errormode))

        startmode = taskData.args.get_arg("startmode")
        OfArgs.append(generate16bitInt(startmode))

        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()
        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"trusted_sec_remote_bofs/sc_create",f"sc_create.{arch}.o")
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

