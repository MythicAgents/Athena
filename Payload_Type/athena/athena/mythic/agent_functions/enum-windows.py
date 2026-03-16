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
