from mythic_payloadtype_container.MythicCommandBase import *
import json


class DirectoryListArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="action",
                cli_name="action",
                display_name="The action to perform",
                description="Load a supported 3rd party library directly into the agent",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Upload"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Download"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Connect"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Disconnect"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="ListSessions"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="SwitchSession"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="ListDirectory"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="ChangeDirectory"
                    ),
                ],
            ),
            CommandParameter(
                name="hostname",
                cli_name="hostname",
                display_name="The connect host",
                description="Load a supported 3rd party library directly into the agent",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Connect"
                    )
                ],
            ),
            CommandParameter(
                name="username",
                cli_name="username",
                display_name="The username to login with",
                description="Load a supported 3rd party library directly into the agent",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Connect"
                    )
                ],
            ),
            CommandParameter(
                name="password",
                cli_name="password",
                display_name="Password",
                description="The user password/key passphrase",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Connect"
                    )
                ],
            ),
            CommandParameter(
                name="keypath",
                cli_name="keypath",
                display_name="Key Path",
                description="Key Path",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Connect"
                    )
                ],
            ),
            CommandParameter(
                name="path",
                cli_name="path",
                display_name="path",
                description="Path to opperator on",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Upload",
                        ui_position=1
                    ),
                    ParameterGroupInfo(
                        required=True,
                        group_name="Download",
                        ui_position=1
                    ),
                    ParameterGroupInfo(
                        required=False,
                        group_name="ChangeDirectory",
                        ui_position=1
                    ),
                ],   
            ),
            CommandParameter(
                name="session",
                cli_name="session",
                display_name="session",
                description="Session to perform action on",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    )
                ],   
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                temp_json = json.loads(self.command_line)
                if "host" in temp_json:
                    # this means we have tasking from the file browser rather than the popup UI
                    # the apfell agent doesn't currently have the ability to do _remote_ listings, so we ignore it
                    self.add_arg("path", temp_json["path"] + "/" + temp_json["file"])
                    self.add_arg("file_browser", True, type=ParameterType.Boolean)
                else:
                    self.add_arg("path", temp_json["path"])
            else:
                self.add_arg("path", self.command_line)
        else:
            self.add_arg("path", ".")


class DirectoryListCommand(CommandBase):
    cmd = "ls"
    needs_admin = False
    help_cmd = "ls [/path/to/directory]"
    description = "Get a directory listing of the requested path, or the current one if none provided."
    version = 1
    is_exit = False
    is_file_browse = True
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    supported_ui_features = ["file_browser:list"]
    author = "@checkymander"
    argument_class = DirectoryListArguments
    attackmapping = ["T1106", "T1083"]
    browser_script = [BrowserScript(script_name="ls", author="@tr41nwr3ck", for_new_ui=True)]
    attributes = CommandAttributes(
        load_only=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
