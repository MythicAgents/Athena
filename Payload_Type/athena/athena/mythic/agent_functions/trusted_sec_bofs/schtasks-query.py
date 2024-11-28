from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
import json

class SchtasksQueryArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="taskpath",
                type=ParameterType.String,
                description="The path of the scheduled task to enumerate",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        default_value=""
                        )
                    ],
            ),
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="Hostname to enumerate the scheduled task of",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
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



    

class SchtasksQueryCommand(CoffCommandBase):
    cmd = "schtasks-query"
    needs_admin = False
    help_cmd = """
    schtasks-query -taskName \\Microsoft\\Windows\\MUI\\LpRemove [-hostname myHost 
Note the task name must be given by full path including taskname, ex. \\Microsoft\\Windows\\MUI\\LpRemove
    
Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Situational-Awareness-BOF/ """
    description = "lists the details of the requested task"
    version = 1
    script_only = True
    supported_ui_features = []
    author = "@TrustedSec"
    argument_class = SchtasksQueryArguments
    attackmapping = ["T1053.005"]
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
            OfArgs.append(generateWString(hostname))
        else:
            OfArgs.append(generateWString(""))

        taskpath = taskData.args.get_arg("taskpath")
        OfArgs.append(generateWString(taskpath))

        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()

        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"trusted_sec_bofs/schtasksquery",f"schtasksquery.{arch}.o")
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
