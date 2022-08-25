from mythic_payloadtype_container.MythicCommandBase import *
import json


class FarmerArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="port",
                type=ParameterType.String,
                description="The port to run on",
                parameter_group_info=[ParameterGroupInfo(
                        required=False,
                        ui_position=0,
                        group_name="Default"
                    ),
                ],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("port", self.command_line.split()[0])
        else:
            raise ValueError("Missing arguments")


class FarmerCommand(CommandBase):
    cmd = "farmer"
    needs_admin = False
    help_cmd = "farmer"
    description = "Copy a file from one location to another."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@domchell, @checkymander"
    argument_class = FarmerArguments
    attackmapping = []
    attributes = CommandAttributes(
        load_only=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass