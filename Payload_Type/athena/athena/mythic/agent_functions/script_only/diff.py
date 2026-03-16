from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class DiffArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path", cli_name="path",
                display_name="First File",
                type=ParameterType.String,
                description="First file to compare",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="path2", cli_name="path2",
                display_name="Second File",
                type=ParameterType.String,
                description="Second file to compare",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class DiffCommand(CommandBase):
    cmd = "diff"
    needs_admin = False
    script_only = True
    depends_on = "file-utils"
    plugin_libraries = []
    help_cmd = "diff -path /file1 -path2 /file2"
    description = "Compare two files line by line"
    version = 1
    author = "@checkymander"
    argument_class = DiffArguments
    attackmapping = ["T1005"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="file-utils",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "diff",
                "path": taskData.args.get_arg("path"),
                "path2": taskData.args.get_arg("path2"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
