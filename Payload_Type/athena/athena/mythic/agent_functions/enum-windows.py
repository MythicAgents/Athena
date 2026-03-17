from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json


class EnumWindowsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["get-localgroup", "get-sessions", "get-shares"],
                default_value="get-localgroup",
                description="Windows enumeration action",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
            CommandParameter(
                name="hosts", cli_name="hosts",
                display_name="Hosts",
                type=ParameterType.String,
                default_value="",
                description="Comma separated list of hosts",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
            CommandParameter(
                name="targetlist", cli_name="targetlist",
                display_name="Target List",
                type=ParameterType.String,
                default_value="",
                description="Base64 encoded newline separated list of hosts",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
            CommandParameter(
                name="group", cli_name="group",
                display_name="Group",
                type=ParameterType.String,
                default_value="",
                description="Local group name to enumerate",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
            CommandParameter(
                name="hostname", cli_name="hostname",
                display_name="Hostname",
                type=ParameterType.String,
                default_value="",
                description="Target hostname for local group enumeration",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


class EnumWindowsCommand(CommandBase):
    cmd = "enum-windows"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "enum-windows -action get-localgroup"
    description = "Windows network enumeration (local groups, sessions, shares)"
    version = 1
    author = "@checkymander"
    argument_class = EnumWindowsArguments
    attackmapping = ["T1069", "T1069.001", "T1135"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
