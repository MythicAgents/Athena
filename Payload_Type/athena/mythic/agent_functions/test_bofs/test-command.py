from mythic_payloadtype_container.MythicRPC import *
from mythic_payloadtype_container.MythicCommandBase import *
import json


class TestCommandArguments(TaskArguments):
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


class TestCommandCommand(CommandBase):
    cmd = "TestCommand"
    needs_admin = False
    help_cmd = "TestCommand"
    description = "Perform an ARP scan in your local network."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@checkymander"
    argument_class = TestCommandArguments
    attackmapping = []
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )
    
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        # groupName = task.args.get_parameter_group_name()

        # if groupName == "TargetList":
        #     file_resp = await MythicRPC().execute("get_file",
        #                                           file_id=task.args.get_arg("inputlist"),
        #                                           task_id=task.id,
        #                                           get_contents=True)


        #     if file_resp.status == MythicRPCStatus.Success:
        #         if len(file_resp.response) > 0:
        #             task.args.add_arg("targetlist", file_resp.response[0]["contents"],
        #                               parameter_group_info=[ParameterGroupInfo(group_name="TargetList")])
        #             #task.display_params = f"{file_resp.response[0]['filename']}"
        #         else:
        #             raise Exception("Failed to find that file")
        #     else:
        #         raise Exception("Error from Mythic trying to get file: " + str(file_resp.error))

        return task

    async def process_response(self, response: AgentResponse):
        pass