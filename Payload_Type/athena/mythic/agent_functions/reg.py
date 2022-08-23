from mythic_payloadtype_container.MythicCommandBase import *
import json

class RegArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="action",
                cli_name="action",
                display_name="Action",
                description="The Action to perform with the plugin. [query, add, delete]",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default" # Many Args
                    ),
                ],
            ),
            CommandParameter(
                name="hostname",
                cli_name="hostname",
                display_name="Host Name",
                description="The IP or Hostname to connect to",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default"
                    )
                ],
            ),
            CommandParameter(
                name="keypath",
                cli_name="keypath",
                display_name="Key Path",
                description="The path to the registry values you want to query",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default"
                    )
                ],
            ),
            CommandParameter(
                name="keyvalue",
                cli_name="keyvalue",
                display_name="Key Value",
                description="The value of the registry key you want to add",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default"
                    )
                ],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.load_args_from_cli_string(self.command_line)



class RegCommand(CommandBase):
    cmd = "reg"
    needs_admin = False
    help_cmd = """
    Usage: reg <action> <hostname> <keypath> <keyvalue>
    """
    description = "Interact with a given host using the Registry"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    supported_ui_features = []
    author = "@checkymander"
    argument_class =RegArguments
    attackmapping = ["T1106", "T1083"]
    attributes = CommandAttributes(
        load_only=True,
        supported_os=[SupportedOS.Windows],
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
