from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class WmiArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["query", "installed-software", "defender-status", "startup-items"],
                default_value="query",
                description="WMI operation",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="query", cli_name="query",
                display_name="WQL Query",
                type=ParameterType.String,
                description="WMI Query Language query string",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="ns", cli_name="ns",
                display_name="Namespace",
                type=ParameterType.String,
                description="WMI namespace",
                default_value="root\\cimv2",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class WmiCommand(CommandBase):
    cmd = "wmi"
    needs_admin = False
    depends_on = None
    plugin_libraries = ["System.Management.dll"]
    help_cmd = "wmi -action query -query \"SELECT * FROM Win32_Process\""
    description = "Execute WMI queries (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = WmiArguments
    attackmapping = ["T1047"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
