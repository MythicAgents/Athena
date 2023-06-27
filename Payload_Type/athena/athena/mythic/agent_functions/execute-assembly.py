from mythic_container.MythicCommandBase import *  # import the basics
import clr
import tempfile
import sys
import json  # import any other code you might need
from .athena_utils import message_utilities
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *

from .athena_utils import message_converter

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class ExecuteAssemblyArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="file",
                type=ParameterType.File,
                description="",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                description="",
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2, 
                        required=False
                    )],
            )
        ]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class ExecuteAssemblyCommand(CommandBase):
    cmd = "execute-assembly"
    needs_admin = False
    help_cmd = "execute-assembly"
    description = "Load an arbitrary .NET assembly via Assembly.Load and track the assembly FullName to call for execution with the runassembly command. If assembly is loaded through Apfell's services -> host file, then operators can simply specify the filename from the uploaded file"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = ""
    argument_class = ExecuteAssemblyArguments
    attackmapping = []
    browser_script = None
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        fData = FileData()
        fData.AgentFileId = taskData.args.get_arg("file")
        file = await SendMythicRPCFileGetContent(fData)
        
        if taskData.args.get_arg("arguments") is None:
            taskData.args.add_arg("arguments", "")

        if file.Success:
            file_contents = base64.b64encode(file.Content)
            # temp = tempfile.NamedTemporaryFile()
            # temp.write(file.Content)
            # temp.seek(0)
            # if not await self.can_run(temp.name):
            #     await message_utilities.send_agent_message(message="Cannot run assembly. Check if assembly is .NET Core or .NET Framework", task=taskData.Task)
            #     raise Exception("Cannot run assembly. Check if assembly is .NET Core or .NET Framework")
            # temp.close()
            taskData.args.add_arg("asm", file_contents.decode("utf-8"))
        else:
            raise Exception("Failed to get file contents: " + file.Error)
        
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response


    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
    
    async def can_run(self, path: str) -> bool:
        try:
            print(path)
            clr.AddReference(path)
        except Exception as e:
            print(e)
            print("Returning False")
            return False
    
        try:
            clr.FindAssembly('System.Runtime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a')
            target_framework = '.NET Framework'
            print("Returning False")
            return False
        except Exception as e:
            print(e.with_traceback)
            target_framework = '.NET Core'
            print("Returning True")
            return True

