from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *
from .athena_utils.argument_utilities import (
    load_json_or_get_shorthand,
    require_nonempty_string,
    split_shorthand,
)

class MvArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="source",
                type=ParameterType.String,
                description="Source file to move.",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Source will move to this location",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
        ]

    errorMsg = "Missing required argument: {}"
    async def parse_arguments(self):
        command_line = load_json_or_get_shorthand(
            self, "mv", "mv requires a source and destination"
        )
        if command_line is not None:
            cmds = split_shorthand(command_line, "mv")
            if len(cmds) != 2:
                raise ValueError("mv requires exactly two arguments: source and destination")
            self.add_arg("source", cmds[0])
            self.add_arg("destination", cmds[1])
        require_nonempty_string(self.get_arg("source"), "source and destination", "mv")
        require_nonempty_string(self.get_arg("destination"), "source and destination", "mv")


class MvCommand(CommandBase):
    cmd = "mv"
    needs_admin = False
    help_cmd = "mv"
    description = "Move a file from one location to another."
    version = 1
    author = "@checkymander"
    argument_class = MvArguments
    attackmapping = ["T1106"]
    attributes = CommandAttributes(
    )
    
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        response.DisplayParams = "-Path {} -Destination {}".format(taskData.args.get_arg("source"), taskData.args.get_arg("destination"))
        return response


    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass