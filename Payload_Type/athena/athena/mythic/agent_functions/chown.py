from .athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class ChownArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path", cli_name="path",
                display_name="File Path",
                type=ParameterType.String,
                description="File to change ownership",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="owner", cli_name="owner",
                display_name="Owner UID",
                type=ParameterType.String,
                description="Numeric user ID",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="group", cli_name="group",
                display_name="Group GID",
                type=ParameterType.String,
                description="Numeric group ID",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=2)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class ChownCommand(CommandBase):
    cmd = "chown"
    needs_admin = True
    script_only = True
    depends_on = "file-utils"
    plugin_libraries = []
    help_cmd = "chown -path /path/to/file -owner 1000 -group 1000"
    description = "Change file ownership (Linux/macOS only)"
    version = 1
    author = "@checkymander"
    argument_class = ChownArguments
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
                "action": "chown",
                "path": taskData.args.get_arg("path"),
                "owner": taskData.args.get_arg("owner"),
                "group": taskData.args.get_arg("group"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
