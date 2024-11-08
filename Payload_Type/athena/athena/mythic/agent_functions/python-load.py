from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *


class PyExecArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="file",
                type=ParameterType.File,
                description="Zip file containing the libraries to import",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


class PyExecCommand(CommandBase):
    cmd = "python-load"
    needs_admin = False
    help_cmd = "python"
    description = "Load required libraries into python interpreter"
    version = 1
    author = "@checkymander"
    argument_class = PyExecArguments
    #attackmapping = ["T1005", "T1552.001"]
    attackmapping = []
    attributes = CommandAttributes(
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response

    async def process_response(self, response: AgentResponse):
        pass
