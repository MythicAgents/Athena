from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class GrepArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="content_pattern", cli_name="content_pattern",
                display_name="Pattern",
                type=ParameterType.String,
                description="Regex pattern to search for in file contents",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="path", cli_name="path",
                display_name="Search Path",
                type=ParameterType.String,
                description="Directory to search",
                default_value=".",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="pattern", cli_name="pattern",
                display_name="File Pattern",
                type=ParameterType.String,
                description="Glob pattern for filenames (e.g. *.conf)",
                default_value="*",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
            CommandParameter(
                name="max_depth", cli_name="max_depth",
                display_name="Max Depth",
                type=ParameterType.Number,
                description="Maximum directory depth",
                default_value=10,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=3)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class GrepCommand(CommandBase):
    cmd = "grep"
    needs_admin = False
    script_only = True
    depends_on = "find"
    plugin_libraries = []
    help_cmd = "grep -content_pattern password -path /etc -pattern *.conf"
    description = "Search file contents with regex"
    version = 1
    author = "@checkymander"
    argument_class = GrepArguments
    attackmapping = ["T1083"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="find",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "grep",
                "path": taskData.args.get_arg("path"),
                "pattern": taskData.args.get_arg("pattern"),
                "content_pattern": taskData.args.get_arg("content_pattern"),
                "max_depth": taskData.args.get_arg("max_depth"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
