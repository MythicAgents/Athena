from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class FindArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["find", "grep"],
                default_value="find",
                description="Search mode",
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
                description="Glob pattern for filename matching (e.g. *.conf)",
                default_value="*",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
            CommandParameter(
                name="content_pattern", cli_name="content_pattern",
                display_name="Content Pattern",
                type=ParameterType.String,
                description="Regex pattern to search file contents (grep mode)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=3)
                ]
            ),
            CommandParameter(
                name="max_depth", cli_name="max_depth",
                display_name="Max Depth",
                type=ParameterType.Number,
                description="Maximum directory depth to search",
                default_value=10,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=4)
                ]
            ),
            CommandParameter(
                name="permissions", cli_name="permissions",
                display_name="Permission Filter",
                type=ParameterType.ChooseOne,
                choices=["", "suid", "sgid", "world-writable"],
                default_value="",
                description="Filter by file permission (Linux/macOS only)",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=5)
                ]
            ),
            CommandParameter(
                name="min_size", cli_name="min_size",
                display_name="Min Size (bytes)",
                type=ParameterType.Number,
                description="Minimum file size in bytes (-1 to disable)",
                default_value=-1,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=6)
                ]
            ),
            CommandParameter(
                name="max_size", cli_name="max_size",
                display_name="Max Size (bytes)",
                type=ParameterType.Number,
                description="Maximum file size in bytes (-1 to disable)",
                default_value=-1,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=7)
                ]
            ),
            CommandParameter(
                name="newer_than", cli_name="newer_than",
                display_name="Newer Than",
                type=ParameterType.String,
                description="Only files modified after this date (YYYY-MM-DD)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=8)
                ]
            ),
            CommandParameter(
                name="older_than", cli_name="older_than",
                display_name="Older Than",
                type=ParameterType.String,
                description="Only files modified before this date (YYYY-MM-DD)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=9)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class FindCommand(CommandBase):
    cmd = "find"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "find -path /etc -pattern *.conf"
    description = "Recursive filesystem search with filters"
    version = 1
    author = "@checkymander"
    argument_class = FindArguments
    attackmapping = ["T1083"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = f"{taskData.args.get_arg('pattern')} in {taskData.args.get_arg('path')}"
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
