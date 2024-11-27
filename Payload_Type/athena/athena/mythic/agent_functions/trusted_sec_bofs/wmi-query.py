from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

from ..athena_utils.mythicrpc_utilities import create_mythic_file
from ..athena_utils.bof_utilities import *
import json
import os


class WmiQueryArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="query",
                type=ParameterType.String,
                description="The query to execute",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="The hostname to run the query on",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=False,
                        )
                    ],
            ),
            CommandParameter(
                name="namespace",
                type=ParameterType.String,
                description="The namespace to query",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
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

class WmiQueryCommand(CoffCommandBase):
    cmd = "wmi-query"
    needs_admin = False
    help_cmd = """
Summary: This command runs a general WMI query on either a local or remote machine and displays the results in a comma separated table.
Usage:   wmi-query -query <query> -namespace <namespace> [-hostname <host>]
		 query		- The query to run. The query should be in WQL.
		 hostname	   - Optional. Specifies the remote system to connect to. Do
						not include or use '.' to indicate the command should
						be run on the local system.
		 namespace	- Optional. Specifies the namespace to connect to. This
						defaults to root\\cimv2 if not specified.
Note:	You must have a valid login token for the system specified if not local.

Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Situational-Awareness-BOF/
    """
    description = "This command runs a general WMI query on either a local or remote machine and displays the results in a comma separated table."
    version = 1
    script_only = True
    supported_ui_features = []
    author = "@TrustedSec"
    argument_class = WmiQueryArguments
    attackmapping = ["T1047"]
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
            OfArgs.append(generateWString(hostname))
        else:
            OfArgs.append(generateWString("."))

        namespace = taskData.args.get_arg("namespace")

        if namespace:
            OfArgs.append(generateWString(namespace))
        else:
            OfArgs.append(generateWString("root\\cimv2"))

        query = taskData.args.get_arg("query")
        OfArgs.append(generateWString(query))

        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()
        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"trusted_sec_bofs/wmi_query",f"wmi_query.{arch}.o")
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