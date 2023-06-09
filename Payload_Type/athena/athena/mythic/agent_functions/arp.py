from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *
import json


class ArpArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="cidr",
                type=ParameterType.String,
                default_value = "",
                description="The CIDR to scan",
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        ui_position=1,
                        group_name="Default"
                    )],
            ),
            CommandParameter(
                name="timeout",
                type=ParameterType.Number,
                description="The timeout in seconds",
                default_value = 60,
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default"
                    ),
                    # ParameterGroupInfo(
                    #     required=True,
                    #     ui_position=0,
                    #     group_name="TargetList"
                    # )
                ],
            ),
            # CommandParameter(
            #     name="inputlist",
            #     type=ParameterType.File,
            #     description="List of hosts in a newline separated file",
            #     parameter_group_info=[ParameterGroupInfo(
            #             required=True,
            #             group_name="TargetList"
            #     )]
            # )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("cidr", self.command_line.split()[0])
                self.add_arg("timeout", self.command_line.split()[1])
        else:
            raise ValueError("Missing arguments")


class ArpCommand(CommandBase):
    cmd = "arp"
    needs_admin = False
    help_cmd = "arp"
    description = "Perform an ARP scan in your local network."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@checkymander"
    argument_class = ArpArguments
    attackmapping = []
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )
    
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response

    async def process_response(self, response: AgentResponse):
        pass