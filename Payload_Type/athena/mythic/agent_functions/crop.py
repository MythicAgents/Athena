from mythic_payloadtype_container.MythicCommandBase import *
import json


class FarmerArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="targetLocation",
                type=ParameterType.String,
                description="The location to drop the file",
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default"
                    ),
                ],
                
            ),
            CommandParameter(
                name="targetFilename",
                type=ParameterType.String,
                description="The filename",
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        ui_position=1,
                        group_name="Default"
                    ),
                ],
                
            ),
            CommandParameter(
                name="targetPath",
                type=ParameterType.String,
                description="Webdav path location",
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        ui_position=2,
                        group_name="Default"
                    ),
                ],
                
            ),
            CommandParameter(
                name="targetIcon",
                type=ParameterType.String,
                description="LNK Icon location",
                default_value = "",
                parameter_group_info=[ParameterGroupInfo(
                        required=False,
                        ui_position=3,
                        group_name="Default"
                    ),
                ],
            ),
            CommandParameter(
                name="recurse",
                type=ParameterType.Boolean,
                default_value = False,
                description="write the file to every sub folder of the specified path",
                parameter_group_info=[ParameterGroupInfo(
                        required=False,
                        ui_position=4,
                        group_name="Default"
                    ),
                ],
            ),
            CommandParameter(
                name="clean",
                type=ParameterType.Boolean,
                default_value = False,
                description="remove the file from every sub folder of the specified path",
                parameter_group_info=[ParameterGroupInfo(
                        required=False,
                        ui_position=4,
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
    cmd = "crop"
    needs_admin = False
    help_cmd = "crop"
    description = "Crop stuff"
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