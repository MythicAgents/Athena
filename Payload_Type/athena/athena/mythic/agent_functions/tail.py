from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_utils import message_converter

class TailArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="path to file (no quotes required)",
            ),
            CommandParameter(
                name = "lines",
                type = ParameterType.Number,
                description = "Number of lines to tail",
            ),
            CommandParameter(
                name = "watch",
                type = ParameterType.Boolean,
                description = "Whether to watch the file for changes",
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) == 0:
            raise Exception("Require file path to retrieve contents for.\n\tUsage: {}".format(TailCommand.help_cmd))
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            if self.command_line[0] == '"' and self.command_line[-1] == '"':
                self.command_line = self.command_line[1:-1]
            elif self.command_line[0] == "'" and self.command_line[-1] == "'":
                self.command_line = self.command_line[1:-1]
            self.add_arg("path", self.command_line)


class TailCommand(CommandBase):
    cmd = "tail"
    needs_admin = False
    help_cmd = "cat /path/to/file"
    description = "Read the contents of a file and display it to the user."
    version = 1
    author = "@checkymander"
    argument_class = TailArguments
    attackmapping = ["T1005", "T1039", "T1025"]
    attributes = CommandAttributes(
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        response.DisplayParams = taskData.args.get_arg("path")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
