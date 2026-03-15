from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class CredentialsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=[
                    "dns-cache", "shadow-read", "wifi-profiles",
                    "vault-enum", "dpapi", "lsass-dump", "sam-dump",
                ],
                default_value="dns-cache",
                description="Credential harvesting action",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class CredentialsCommand(CommandBase):
    cmd = "credentials"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "credentials -action dns-cache"
    description = "Credential harvesting (DNS cache, shadow, WiFi profiles, vault)"
    version = 1
    author = "@checkymander"
    argument_class = CredentialsArguments
    attackmapping = ["T1003"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
