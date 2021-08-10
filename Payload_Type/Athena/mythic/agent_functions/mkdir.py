from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class MkdirArguments(TaskArguments):
    def __init__(self, command_line):
        super().__init__(command_line)
        self.args = {
            "path": CommandParameter(
                name="path",
                type=ParameterType.String,
                description="path to file (no quotes required)",
            ),
        }

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("path", self.command_line)
        else:
            raise ValueError("Missing arguments")


class MkdirCommand(CommandBase):
    cmd = "mkdir"
    needs_admin = False
    help_cmd = "mkdir /path/to/folder"
    description = "Creates a folder in the specified path."
    version = 1
    author = "@checkymander"
    argument_class = CatArguments
    attackmapping = ["T1081", "T1106"]

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        resp = await MythicRPC().execute("create_artifact", task_id=task.id,
            artifact="$.NSString.stringWithContentsOfFileEncodingError",
            artifact_type="API Called",
        )
        return task

    async def process_response(self, response: AgentResponse):
        pass