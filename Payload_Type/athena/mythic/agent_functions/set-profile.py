from mythic_payloadtype_container.MythicCommandBase import *
from mythic_payloadtype_container.MythicRPC import *


class SetProfileArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="name",
                type=ParameterType.String,
                description="Profile to begin using",
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("name", self.command_line)
        else:
            raise ValueError("Missing arguments")


class SetProfileCommand(CommandBase):
    cmd = "set-profile"
    needs_admin = False
    help_cmd = "set-profile -name <name>"
    description = "Change the current working directory to another directory. No quotes are necessary and relative paths are fine"
    version = 1
    author = "@checkymander"
    argument_class = SetProfileArguments
    attackmapping = ["T1083"]
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        resp = await MythicRPC().execute("create_artifact", task_id=task.id,
            artifac="fileManager.changeCurrentDirectoryPath",
            artifact_type="API Called",
        )
        return task

    async def process_response(self, response: AgentResponse):
        pass