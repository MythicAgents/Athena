from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *

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
        fData = FileData()
        fData.AgentFileId = taskData.args.get_arg("file")
        file = await SendMythicRPCFileGetContent(fData)
        
        if file.Success:
            file_contents = base64.b64encode(file.Content)
            taskData.args.add_arg("asm", file_contents.decode("utf-8"))
            taskData.args.remove_arg("file")
        else:
            raise Exception("Failed to get file contents: " + file.Error)
            
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp

