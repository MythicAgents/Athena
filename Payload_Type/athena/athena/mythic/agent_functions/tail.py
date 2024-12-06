from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *

class TailArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="path to file (no quotes required)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0
                    )
                ]
            ),
            CommandParameter(
                name = "lines",
                type = ParameterType.Number,
                description = "Number of lines to tail",
                default_value=5,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    )
                ]
            ),
            CommandParameter(
                name = "watch",
                type = ParameterType.Boolean,
                description = "Whether to watch the file for changes",
                default_value=False,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=2
                    )
                ]
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
    help_cmd = """tail /path/to/file [-lines 10] [-watch=true]"""
    description = "Read the end n lines of a file and display to the user."
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
        if taskData.args.get_arg("watch"):
            response.DisplayParams=f"to watch {taskData.args.get_arg('path')}"
        else:
            response.DisplayParams = f"{taskData.args.get_arg('lines')} lines of {taskData.args.get_arg('path')}"
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass