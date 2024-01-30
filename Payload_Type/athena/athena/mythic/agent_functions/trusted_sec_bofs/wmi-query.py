from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json
import binascii
import cmd 
import struct
import os
import subprocess



class OfArg:
    def __init__(self, arg_data, arg_type):
        self.arg_data = arg_data
        self.arg_type = arg_type

def generateWString(arg):
    return OfArg(arg.encode('utf-16le') + b'\x00\x00', 0)

def generateString(arg):
    return OfArg(arg.encode('ascii') + b'\x00', 0)

def generate32bitInt(arg):
    return OfArg(struct.pack('<I', int(arg)), 1)

def generate16bitInt(arg):
    return OfArg(struct.pack('<H', int(arg)), 2)

def dobinarystuff(arg):
    return OfArg(arg)

def SerialiseArgs(OfArgs):
    output_bytes = b''
    for of_arg in OfArgs:
        output_bytes += struct.pack('<I', of_arg.arg_type)
        output_bytes += struct.pack('<I', len(of_arg.arg_data))
        output_bytes += of_arg.arg_data
    return output_bytes

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

class WmiQueryCommand(CommandBase):
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


        bof_path = f"/Mythic/athena/mythic/agent_functions/trusted_sec_bofs/wmi_query/wmi_query.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await self.compile_bof("/Mythic/athena/mythic/agent_functions/trusted_sec_bofs/wmi_query/")

        # Read the COFF file from the proper directory
        with open(bof_path, "rb") as coff_file:
            encoded_file = base64.b64encode(coff_file.read())

        # Upload the COFF file to Mythic, delete after using so that we don't have a bunch of wasted space used
        file_resp = await MythicRPC().execute("create_file",
                                   task_id=taskData.Task.ID,
                                    file=encoded_file,
                                    delete_after_fetch=True)  
        
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

        encoded_args = base64.b64encode(SerialiseArgs(OfArgs)).decode()

        resp = await MythicRPC().execute("create_subtask_group", tasks=[
            {"command": "coff", "params": {"coffFile":file_resp.response["agent_file_id"], "functionName":"go","arguments": encoded_args, "timeout":"60"}},
            ], 
            subtask_group_name = "coff", parent_task_id=taskData.Task.ID)

        # We did it!
        return response

    async def process_response(self, response: AgentResponse):
        pass

    async def compile_bof(self, bof_path):
        p = subprocess.Popen(["make"], cwd=bof_path)
        p.wait()
        streamdata = p.communicate()[0]
        rc = p.returncode
        if rc != 0:
            raise Exception("Error compiling BOF: " + str(streamdata))
