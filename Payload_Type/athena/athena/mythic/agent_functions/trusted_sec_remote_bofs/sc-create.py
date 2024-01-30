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

class ScCreateCommand(CommandBase):
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


        bof_path = f"/Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/sc_create/sc_create.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await self.compile_bof("/Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/sc_create/")

        # Read the COFF file from the proper directory
        with open(bof_path, "rb") as coff_file:
            encoded_file = base64.b64encode(coff_file.read())

        # Upload the COFF file to Mythic, delete after using so that we don't have a bunch of wasted space used
        file_resp = await MythicRPC().execute("create_file",
                                   task_id=taskData.Task.ID,
                                    file=encoded_file,
                                    delete_after_fetch=True)  
        
       ######################################################
       # To do add arguments for the bof
       ######################################################
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
