from mythic_payloadtype_container.MythicCommandBase import *
import json

class MvArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="source",
                type=ParameterType.String,
                description="Source file to move.",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Source will move to this location",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("source", self.command_line.split()[0])
                self.add_arg("destination", self.command_line.split()[1])
        else:
            raise ValueError("Missing arguments")


class MvCommand(CommandBase):
    cmd = "mv"
    needs_admin = False
    help_cmd = "mv"
    description = "Move a file from one location to another."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@checkymander"
    argument_class = MvArguments
    attackmapping = ["T1106"]
    attributes = CommandAttributes(
        load_only=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass