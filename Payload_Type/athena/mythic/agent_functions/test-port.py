from mythic_payloadtype_container.MythicRPC import *
from mythic_payloadtype_container.MythicCommandBase import *
import json


class TestportArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="hosts",
                type=ParameterType.String,
                description="The hosts to check (comma separated)",
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        ui_position=1,
                        group_name="Default"
                    )],
            ),
            CommandParameter(
                name="ports",
                type=ParameterType.String,
                description="TCP ports to check (comma separated)",
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="TargetList"
                    )
                ],
            ),
            CommandParameter(
                name="inputlist",
                type=ParameterType.File,
                description="List of hosts in a newline separated file",
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        group_name="TargetList"
                )]
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hosts", self.command_line.split()[0])
                self.add_arg("ports", self.command_line.split()[1])
        else:
            raise ValueError("Missing arguments")


class TestportCommand(CommandBase):
    cmd = "test-port"
    needs_admin = False
    help_cmd = "test-port"
    description = "Check if a list of ports are open against a host/list of hosts."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@checkymander"
    argument_class = TestportArguments
    attackmapping = []
    attributes = CommandAttributes(
        load_only=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        groupName = task.args.get_parameter_group_name()
        if groupName == "TargetList":
            file_resp = await MythicRPC().execute("get_file",
                                                  file_id=task.args.get_arg("inputlist"),
                                                  task_id=task.id,
                                                  get_contents=True)
            if file_resp.status == MythicRPCStatus.Success:
                if len(file_resp.response) > 0:
                    task.args.add_arg("targetlist", file_resp.response[0]["contents"],
                                      parameter_group_info=[ParameterGroupInfo(group_name="Default")])
                    #task.display_params = f"{file_resp.response[0]['filename']}"
                else:
                    raise Exception("Failed to find that file")
            else:
                raise Exception("Error from Mythic trying to get file: " + str(file_resp.error))
        else:
            return task

    async def process_response(self, response: AgentResponse):
        pass