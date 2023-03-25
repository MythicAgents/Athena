from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
import base64

from .athena_messages import message_converter

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class CoffArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="coffFile",
                type=ParameterType.File,
                description="Upload COFF file to be executed (typically ends in .o)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=1,
                        )
                    ],
            ),
            CommandParameter(
                name="functionName",
                type=ParameterType.String,
                description="Name of entry function to execute in COFF",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=True,
                        default_value="go",
                        )
                    ],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                description="Arguments converted to bytes using beacon_compatibility.py",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
                        required=False,
                        default_value="go"
                        )
                    ],
            ),
            CommandParameter(
                name="timeout",
                type=ParameterType.String,
                description="Time to wait for the coff file to execute before killing it",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
                        required=False,
                        default_value="30"
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
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@checkymander & @scottctaylor12"
    argument_class = CoffArguments
    attackmapping = []
    attributes = CommandAttributes(
        load_only=False,
        builtin=False
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:        
        fData = FileData()
        fData.AgentFileId = task.args.get_arg("coffFile")
        file = await SendMythicRPCFileGetContent(fData)
        
        if file.Success:
            file_contents = base64.b64encode(file.Content)
            decoded_buffer = base64.b64decode(file_contents)
            task.args.add_arg("fileSize", f"{len(decoded_buffer)}")
            task.args.add_arg("asm", file_contents.decode("utf-8"))
        else:
            raise Exception("Failed to get file contents: " + file.Error)

        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
