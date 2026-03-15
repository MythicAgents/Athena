from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class SysinfoArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=[
                    "sysinfo", "id", "container-detect",
                    "mount", "package-list", "dotnet-versions",
                ],
                default_value="sysinfo",
                description="Info category to retrieve",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class SysinfoCommand(CommandBase):
    cmd = "sysinfo"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "sysinfo -action sysinfo"
    description = "Gather system information (OS, CPU, RAM, user, containers, mounts, packages)"
    version = 1
    author = "@checkymander"
    argument_class = SysinfoArguments
    attackmapping = ["T1082"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
