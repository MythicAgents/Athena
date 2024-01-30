from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_utils import message_converter

class RegArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
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
                name="keyPath",
                cli_name="keypath",
                display_name="Key Path",
                description="The path to the registry values you want to perform the action against",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=1,
                        group_name="Default"
                    )
                ],
            ),
            CommandParameter(
                name="keyName",
                cli_name="keyname",
                display_name="Key Name",
                description="The name of the subkey",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=2,
                    )
                ],
            ),
            CommandParameter(
                name="keyValue",
                cli_name="keyvalue",
                display_name="Key Value",
                description="The value of the subkey to set",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=3,
                    )
                ],
            ),
            CommandParameter(
                name="keyType",
                cli_name="keyType",
                display_name="Key Type",
                description="The type of type of registry key to set",
                type=ParameterType.ChooseOne,
                default_value = "string",
                choices=[
                    "string",
                    "dword",
                    "qword",
                    "binary",
                    "multi_string",
                    "expand_string",
                ],
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=4,
                    )
                ],
            ),
            CommandParameter(
                name="hostName",
                cli_name="hostname",
                display_name="Host Name",
                description="The IP or Hostname to connect to for remote reg",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        ui_position=5,
                        group_name="Default",
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
    reg query HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Run
    reg add -keyPath=HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run -keynNme=MyFakeApplication -keyValue=C:\\Temp\\Athena.exe
    reg delete -keyPath=HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run -keyMame=MyFakeApplication
    """
    description = "Interact with a given host using the Registry. only HKLM and HKU can be accessed remotely"
    version = 1
    supported_ui_features = []
    author = "@checkymander"
    argument_class =RegArguments
    attackmapping = ["T1106", "T1083"]
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
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
