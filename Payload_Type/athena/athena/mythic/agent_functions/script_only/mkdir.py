from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class MkdirArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="path to file (no quotes required)",
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) == 0:
            raise Exception(
                "No path given.\n\tUsage: {}".format(
                    MkdirCommand.help_cmd))
        if self.command_line[0] == '"' and self.command_line[-1] == '"':
            self.command_line = self.command_line[1:-1]
        elif self.command_line[0] == "'" and self.command_line[-1] == "'":
            self.command_line = self.command_line[1:-1]
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            self.add_arg("path", self.command_line)


class MkdirCommand(CommandBase):
    cmd = "mkdir"
    needs_admin = False
    script_only = True
    depends_on = "file-utils"
    plugin_libraries = []
    help_cmd = "mkdir /path/to/folder"
    description = "Creates a folder in the specified path."
    version = 1
    author = "@checkymander"
    argument_class = MkdirArguments
    attackmapping = []
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="file-utils",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "mkdir",
                "path": taskData.args.get_arg("path"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        resp = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        resp.DisplayParams = "-Path {}".format(
            taskData.args.get_arg("path"))
        return resp

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
