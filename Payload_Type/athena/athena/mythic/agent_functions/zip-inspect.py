from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *
from .athena_utils.argument_utilities import (
    load_json_or_get_shorthand,
    require_nonempty_string,
    split_shorthand,
)

class ZipInspectArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="Source zip to inspect.",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            )
        ]

    async def parse_arguments(self):
        command_line = load_json_or_get_shorthand(
            self, "zip-inspect", "zip-inspect requires a path"
        )
        if command_line is not None:
            values = split_shorthand(command_line, "zip-inspect")
            if len(values) != 1:
                raise ValueError("zip-inspect requires exactly one path")
            self.add_arg("path", values[0])
        require_nonempty_string(self.get_arg("path"), "path", "zip-inspect")


class ZipInspectCommand(CommandBase):
    cmd = "zip-inspect"
    needs_admin = False
    help_cmd = "zip-inspect <path>"
    description = "Inspect the contents of a zip file."
    version = 1
    author = "@checkymander"
    argument_class = ZipInspectArguments
    attackmapping = ["T1570"]
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
        pass