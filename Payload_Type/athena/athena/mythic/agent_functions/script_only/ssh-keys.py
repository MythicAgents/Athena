from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class SshKeysArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path", cli_name="path",
                display_name="Search Path",
                type=ParameterType.String,
                description="Additional directory to search for SSH keys",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("path", self.command_line.strip())

class SshKeysCommand(CommandBase):
    cmd = "ssh-keys"
    needs_admin = False
    script_only = True
    depends_on = "ssh-recon"
    plugin_libraries = []
    help_cmd = "ssh-keys"
    description = "Find SSH private/public keys"
    version = 1
    author = "@checkymander"
    argument_class = SshKeysArguments
    attackmapping = ["T1552.004"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="ssh-recon",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "ssh-keys",
                "path": taskData.args.get_arg("path"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
