from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class ZipInspectArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="Path to zip file to inspect.",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            )
        ]

    async def parse_arguments(self):
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            self.add_arg("path", self.command_line)


class ZipInspectCommand(CommandBase):
    cmd = "zip-inspect"
    needs_admin = False
    script_only = True
    depends_on = "zip"
    help_cmd = "zip-inspect <path_to_zip>"
    description = "Inspect the contents of a zip file"
    version = 2
    author = "@checkymander"
    argument_class = ZipInspectArguments
    attackmapping = ["T1570"]
    attributes = CommandAttributes()
    completion_functions = {
        "command_callback": default_completion_callback
    }

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="zip",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "inspect",
                "path": taskData.args.get_arg("path"),
            }),
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
