from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

class FileUtilsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["head", "touch", "wc", "diff", "link", "chmod", "chown", "cat", "cp", "mv", "rm", "mkdir", "tail", "timestomp"],
                default_value="head",
                description="File utility action",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="path", cli_name="path",
                display_name="Path",
                type=ParameterType.String,
                description="File path",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="path2", cli_name="path2",
                display_name="Second Path",
                type=ParameterType.String,
                description="Second file path (for diff, link)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
            CommandParameter(
                name="lines", cli_name="lines",
                display_name="Lines",
                type=ParameterType.Number,
                description="Number of lines (for head, tail)",
                default_value=10,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=3)
                ]
            ),
            CommandParameter(
                name="source", cli_name="source",
                display_name="Source",
                type=ParameterType.String,
                description="Source path (for cp, mv, timestomp)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=4)
                ]
            ),
            CommandParameter(
                name="destination", cli_name="destination",
                display_name="Destination",
                type=ParameterType.String,
                description="Destination path (for cp, mv, timestomp)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=5)
                ]
            ),
            CommandParameter(
                name="watch", cli_name="watch",
                display_name="Watch",
                type=ParameterType.Boolean,
                description="Watch file for changes (for tail)",
                default_value=False,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=6)
                ]
            ),
            CommandParameter(
                name="host", cli_name="host",
                display_name="Host",
                type=ParameterType.String,
                description="Remote host (for rm)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=7)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class FileUtilsCommand(CommandBase):
    cmd = "file-utils"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "file-utils -action head -path /etc/passwd -lines 10"
    description = "File utilities: head, touch, wc, diff, link, chmod, chown, cat, cp, mv, rm, mkdir, tail, timestomp"
    version = 1
    author = "@checkymander"
    argument_class = FileUtilsArguments
    attackmapping = ["T1005"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
