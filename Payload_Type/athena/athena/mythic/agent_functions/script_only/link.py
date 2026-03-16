from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class LinkArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path", cli_name="path",
                display_name="Target Path",
                type=ParameterType.String,
                description="Target file the link points to",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="path2", cli_name="path2",
                display_name="Link Path",
                type=ParameterType.String,
                description="Path where the link will be created",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="link_type", cli_name="link_type",
                display_name="Link Type",
                type=ParameterType.ChooseOne,
                choices=["symbolic"],
                default_value="symbolic",
                description="Type of link to create",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class LinkCommand(CommandBase):
    cmd = "link"
    needs_admin = False
    script_only = True
    depends_on = "file-utils"
    plugin_libraries = []
    help_cmd = "link -path /target -path2 /linkname"
    description = "Create a symbolic link"
    version = 1
    author = "@checkymander"
    argument_class = LinkArguments
    attackmapping = ["T1547"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="file-utils",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "link",
                "path": taskData.args.get_arg("path"),
                "path2": taskData.args.get_arg("path2"),
                "link_type": taskData.args.get_arg("link_type"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
