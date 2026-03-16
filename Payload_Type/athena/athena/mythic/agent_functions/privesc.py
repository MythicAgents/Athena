from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class PrivescArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["privcheck", "service-enum"],
                default_value="privcheck",
                description="Privilege escalation check action",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class PrivescCommand(CommandBase):
    cmd = "privesc"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "privesc -action privcheck"
    description = "Privilege escalation checks (token privileges, service misconfigurations)"
    version = 1
    author = "@checkymander"
    argument_class = PrivescArguments
    attackmapping = ["T1078.003"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows]
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
