from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *

from .athena_utils.mythicrpc_utilities import *

from .athena_utils import message_converter

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class Shellcoderguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="file",
                type=ParameterType.File,
                description="",
            ),
        ]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class ShellcodeCommand(CommandBase):
    cmd = "shellcode"
    needs_admin = False
    help_cmd = "shellcode"
    description = "Load a buffer into the process and execute it"
    version = 1
    author = "@checkymander"
    argument_class = Shellcoderguments
    attackmapping = ["T1620"]
    browser_script = None
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )

        encoded_file_contents = await get_mythic_file(taskData.args.get_arg("file"))
        original_file_name = await get_mythic_file_name(taskData.args.get_arg("file"))
        taskData.args.add_arg("asm", encoded_file_contents) 
        response.DisplayParams = original_file_name
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
