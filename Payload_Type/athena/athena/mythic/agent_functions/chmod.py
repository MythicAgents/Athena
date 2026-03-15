from .athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class ChmodArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path", cli_name="path",
                display_name="File Path",
                type=ParameterType.String,
                description="File to change permissions",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="mode", cli_name="mode",
                display_name="Mode",
                type=ParameterType.String,
                description="Octal permission mode (e.g. 755)",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class ChmodCommand(CommandBase):
    cmd = "chmod"
    needs_admin = False
    script_only = True
    depends_on = "file-utils"
    plugin_libraries = []
    help_cmd = "chmod -path /path/to/file -mode 755"
    description = "Change file permissions (Linux/macOS only)"
    version = 1
    author = "@checkymander"
    argument_class = ChmodArguments
    attackmapping = ["T1222"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="file-utils",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "chmod",
                "path": taskData.args.get_arg("path"),
                "mode": taskData.args.get_arg("mode"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
