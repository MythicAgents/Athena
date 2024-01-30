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

def gernerateBinary(arg):
    return OfArg(arg)

def SerialiseArgs(OfArgs):
    output_bytes = b''
    for of_arg in OfArgs:
        output_bytes += struct.pack('<I', of_arg.arg_type)
        output_bytes += struct.pack('<I', len(of_arg.arg_data))
        output_bytes += of_arg.arg_data
    return output_bytes

class SchTasksCreateArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="taskfile",
                type=ParameterType.File,
                description="Required. The file for the created task.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="taskpath",
                type=ParameterType.String,
                description="Required. The path for the created task.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="usermode",
                type=ParameterType.String,
                description="Required. The username to associate with the task. (user, xml, system)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="forcemode",
                type=ParameterType.String,
                description="Required. Creation disposition. (create, update)",
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
                description="Optional. The system on which to create the task.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
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

class SchTasksCreateCommand(CommandBase):
    cmd = "schtasks-create"
    needs_admin = False
    help_cmd = "schtasks-create"
    description = "Enumerate CAs and templates in the AD using Win32 functions (Created by TrustedSec)"
    version = 1
    script_only = True
    supported_ui_features = ["T1053.005"]
    author = "@TrustedSec"
    argument_class = SchTasksCreateArguments
    attackmapping = []
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        usermode = {"user":0,"system":1,"xml":2}
        forcemode = {"create":0,"update":1}
        # Get our architecture version
        arch = taskData.Callback.Architecture


        if(arch=="x86"):
            raise Exception("BOF's are currently only supported on x64 architectures")

        fData = FileData()
        fData.AgentFileId = taskData.args.get_arg("taskfile")
        file = await SendMythicRPCFileGetContent(fData)
        groupName = taskData.args.get_parameter_group_name()
        if file.Success:
            file_contents = file.Content
        else:
            raise Exception("Failed to get file contents: " + file.Error)


        strMode = taskData.args.get_arg("usermode")
        forceMode = taskData.args.get_arg("forcemode")
        if(strMode.lower() not in usermode):
            raise Exception("Invalid forcemode. Must be user, xml, or system")
        if(forceMode.lower() not in forcemode):
            raise Exception("Invalid forcemode. Must be create or update")

        mode = usermode[strMode.lower()]
        force = forcemode[forceMode.lower()]

        encoded_args = ""
        OfArgs = []
        OfArgs.append(generateWString(taskData.args.get_arg("hostname"))) # Z
        OfArgs.append(generateWString(taskData.args.get_arg("taskpath"))) # Z
        OfArgs.append(generateWString(file_contents.decode())) # Z
        OfArgs.append(generate32bitInt(mode)) # i
        OfArgs.append(generate32bitInt(force)) # i  

        bof_path = f"/Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/schtaskscreate/schtaskscreate.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await self.compile_bof("/Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/schtaskscreate/")

        # Read the COFF file from the proper directory
        with open(bof_path, "rb") as coff_file:
            encoded_file = base64.b64encode(coff_file.read())

        # Upload the COFF file to Mythic, delete after using so that we don't have a bunch of wasted space used
        file_resp = await MythicRPC().execute("create_file",
                                    task_id=taskData.Task.ID,
                                    file=encoded_file,
                                    delete_after_fetch=True)  

        encoded_args = base64.b64encode(SerialiseArgs(OfArgs)).decode()

        resp = await MythicRPC().execute("create_subtask_group", tasks=[
            {"command": "coff", "params": {"coffFile":file_resp.response["agent_file_id"], "functionName":"go","arguments": encoded_args, "timeout":"60"}},
            ], 
            subtask_group_name = "coff", parent_task_id=taskData.Task.ID)

        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp

    async def compile_bof(self, bof_path):
        p = subprocess.Popen(["make"], cwd=bof_path)
        p.wait()
        streamdata = p.communicate()[0]
        rc = p.returncode
        if rc != 0:
            raise Exception("Error compiling BOF: " + str(streamdata))
