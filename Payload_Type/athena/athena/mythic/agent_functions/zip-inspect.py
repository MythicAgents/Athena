from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from .athena_utils import message_converter
import json


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
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            self.add_arg("path", self.command_line)


class ZipInspectCommand(CommandBase):
    cmd = "zip-inspect"
    needs_admin = False
    help_cmd = "cp <source> <destination>"
    description = "Copy a file from one location to another."
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