from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class Base64Arguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path", cli_name="path",
                display_name="File Path",
                type=ParameterType.String,
                description="File to base64 encode/decode",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="encode", cli_name="encode",
                display_name="Encode",
                type=ParameterType.Boolean,
                default_value=True,
                description="True to encode, False to decode",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=1)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class Base64Command(CommandBase):
    cmd = "base64"
    needs_admin = False
    script_only = True
    depends_on = "hash"
    plugin_libraries = []
    help_cmd = "base64 -path /path/to/file -encode true"
    description = "Base64 encode or decode a file"
    version = 1
    author = "@checkymander"
    argument_class = Base64Arguments
    attackmapping = ["T1140"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="hash",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "base64",
                "path": taskData.args.get_arg("path"),
                "encode": taskData.args.get_arg("encode"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
