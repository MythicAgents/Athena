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

class GetMachineAccountArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="computername",
                type=ParameterType.String,
                description="Name of the machine account to add",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        default_value=""
                        )
                    ],
            ),
            CommandParameter(
                name="password",
                type=ParameterType.String,
                description="Password of the machine account to add",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
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


    

class GetMachineAccountCommand(CommandBase):
    cmd = "add-machine-account"
    needs_admin = False
    help_cmd = """add-machine-account -computername MyComputer [-password P@ssw0rd]
    
Credit: The Outflank team for the original BOF - https://github.com/outflanknl/C2-Tool-Collection
    """
    description = "Add a computer account to the Active Directory domain."
    version = 1
    script_only = True
    supported_ui_features = ["T1136.002"]
    author = "Cornelis de Plaa (@Cn33liz)"
    argument_class = GetMachineAccountArguments
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


        bof_path = f"/Mythic/athena/mythic/agent_functions/outflank_bofs/add_machine_account/AddMachineAccount.o"
        if(os.path.isfile(bof_path) == False):
            await self.compile_bof("/Mythic/athena/mythic/agent_functions/outflank_bofs/add_machine_account/")

        # Read the COFF file from the proper directory
        with open(bof_path, "rb") as coff_file:
            encoded_file = base64.b64encode(coff_file.read())

        # Upload the COFF file to Mythic, delete after using so that we don't have a bunch of wasted space used
        file_resp = await MythicRPC().execute("create_file",
                                    task_id=taskData.Task.ID,
                                    file=encoded_file,
                                    delete_after_fetch=True)  
        
        # Initialize our Argument list object
        OfArgs = []
        
        #Pack our argument and add it to the list
        computername = taskData.args.get_arg("computername")

        #Repeat this for every argument being passed to the COFF (Changing the type as needed)
        OfArgs.append(generateWString(computername))


        password = taskData.args.get_arg("password")
        if password:
            OfArgs.append(generateWString(password))
        else:
            OfArgs.append(generateWString(""))

        # Serialize our arguments into a single buffer and base64 encode it
        encoded_args = base64.b64encode(SerialiseArgs(OfArgs)).decode()

        # Delegate the execution to the coff command, passing: 
        #   the file_id from our create_file RPC call
        #   the functionName which in this case is go
        #   the number of arguments we packed which in this task is 1
        #   the arguments as a base64 encoded string generated by the OfArgs class
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
