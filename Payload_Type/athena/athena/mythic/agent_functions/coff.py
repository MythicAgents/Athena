from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
import base64
from .athena_utils import message_converter

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class CoffArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="coffFile",
                type=ParameterType.File,
                description="Upload COFF file to be executed (typically ends in .o)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        )
                    ],
            ),
            CommandParameter(
                name="functionName",
                type=ParameterType.String,
                description="Name of entry function to execute in COFF",
                default_value="go",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                description="Arguments converted to bytes using beacon_compatibility.py",
                default_value="go",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=False,
                        )
                    ],
            ),
            CommandParameter(
                name="timeout",
                type=ParameterType.String,
                description="Time to wait for the coff file to execute before killing it",
                default_value="30",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=3,
                        required=False,
                        )
                    ],
            ),
        ]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class CoffCommand(CommandBase):
    cmd = "coff"
    needs_admin = False
    help_cmd = "coff"
    description = "Execute a COFF file in process. Leverages the Netitude RunOF project. argumentData can be generated using the beacon_generate.py script found in the TrustedSec COFFLoader GitHub repo. This command is not intended to be used directly, but can be."
    version = 1
    author = "@checkymander & @scottctaylor12"
    argument_class = CoffArguments
    attackmapping = ["T1620"]
    attributes = CommandAttributes(
        load_only=False,
        builtin=False,
        supported_os=[SupportedOS.Windows],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        fData = FileData()
        fData.AgentFileId = taskData.args.get_arg("coffFile")
        file = await SendMythicRPCFileGetContent(fData)
        if file.Success:
            file_contents = base64.b64encode(file.Content)
            decoded_buffer = base64.b64decode(file_contents)
            taskData.args.add_arg("fileSize", f"{len(decoded_buffer)}")
            taskData.args.add_arg("asm", file_contents.decode("utf-8"))
        else:
            raise Exception("Failed to get file contents: " + file.Error)
        
        response.DisplayParams = ""

        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
