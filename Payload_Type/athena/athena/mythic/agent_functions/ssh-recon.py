from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class SshReconArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["ssh-keys", "authorized-keys-read", "known-hosts"],
                default_value="ssh-keys",
                description="SSH recon action",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="path", cli_name="path",
                display_name="Path",
                type=ParameterType.String,
                description="Optional path override (default: ~/.ssh/)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=1)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class SshReconCommand(CommandBase):
    cmd = "ssh-recon"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "ssh-recon -action ssh-keys"
    description = "SSH key enumeration and authorized_keys reading"
    version = 1
    author = "@checkymander"
    argument_class = SshReconArguments
    attackmapping = ["T1552.004"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
