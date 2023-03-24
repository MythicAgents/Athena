from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_messages import message_converter


class CatArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="path to file (no quotes required)",
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("path", self.command_line)
        else:
            raise ValueError("Missing arguments")


class CatCommand(CommandBase):
    cmd = "cat"
    needs_admin = False
    help_cmd = "cat /path/to/file"
    description = "Read the contents of a file and display it to the user."
    version = 1
    author = "@checkymander"
    argument_class = CatArguments
    attackmapping = ["T1005", "T1552.001"]
    #completion_functions: dict[str, Callable[[PTTaskCompletionFunctionMessage], Awaitable[PTTaskCompletionFunctionMessageResponse]]] = {}
    #attackmapping = []
    attributes = CommandAttributes(
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        task.completed_callback_function = "functionName"
        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        await MythicRPC().execute("create_output", task_id=task.Task.ID, output=translateAthenaMessage(resp.))
        return resp
