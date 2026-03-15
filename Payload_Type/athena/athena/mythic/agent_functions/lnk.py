from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class LnkArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["add", "update"],
                default_value="add",
                description="Create or update a shortcut",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="path", cli_name="path",
                display_name="Shortcut Path",
                type=ParameterType.String,
                description="Path to the .lnk shortcut",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="target", cli_name="target",
                display_name="Target",
                type=ParameterType.String,
                description="Target path for the shortcut",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class LnkCommand(CommandBase):
    cmd = "lnk"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "lnk -action add -path C:\\Users\\user\\Desktop\\test.lnk -target C:\\Windows\\System32\\calc.exe"
    description = "Create or modify Windows shortcuts (Windows only)"
    version = 2
    author = "@checkymander"
    argument_class = LnkArguments
    attackmapping = ["T1547.009"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
