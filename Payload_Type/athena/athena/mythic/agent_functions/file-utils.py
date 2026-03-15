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
                choices=["head", "touch", "wc", "diff", "link", "chmod", "chown"],
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
                description="Number of lines (for head)",
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

class FileUtilsCommand(CommandBase):
    cmd = "file-utils"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "file-utils -action head -path /etc/passwd -lines 10"
    description = "File utilities: head, touch, wc, diff, link, chmod, chown"
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
