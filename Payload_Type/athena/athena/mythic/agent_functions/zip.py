from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *
from .athena_utils.argument_utilities import (
    load_json_or_get_shorthand,
    require_nonempty_string,
    split_shorthand,
)

class ZipArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="source",
                type=ParameterType.String,
                description="Source file to copy.",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Source will copy to this location",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
        ]

    async def parse_arguments(self):
        command_line = load_json_or_get_shorthand(
            self, "zip", "zip requires a source and destination"
        )
        if command_line is not None:
            cmds = split_shorthand(command_line, "zip")
            if len(cmds) != 2:
                raise ValueError("zip requires exactly two arguments: source and destination")
            self.add_arg("source", cmds[0])
            self.add_arg("destination", cmds[1])
        require_nonempty_string(self.get_arg("source"), "source and destination", "zip")
        require_nonempty_string(self.get_arg("destination"), "source and destination", "zip")


class ZipCommand(CommandBase):
    cmd = "zip"
    needs_admin = False
    help_cmd = "zip <source> <destination>"
    description = "Copy a file from one location to another."
    version = 1
    author = "@checkymander"
    argument_class = ZipArguments
    attackmapping = ["T1570"]
    attributes = CommandAttributes(
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        response.DisplayParams = "-Source {} -Destination {}".format(
            taskData.args.get_arg("source"), taskData.args.get_arg("destination")
        )
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass