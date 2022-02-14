from mythic_payloadtype_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_payloadtype_container.MythicRPC import *

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
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
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
    is_download_file = True
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

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        file_resp = await MythicRPC().execute("get_file",
                                              file_id=task.args.get_arg("file"),
                                              task_id=task.id,
                                              get_contents=True)
        if file_resp.status == MythicRPCStatus.Success:
            if len(file_resp.response) > 0:
                task.args.add_arg("assembly", file_resp.response[0]["contents"])
                task.display_params = f"{file_resp.response[0]['filename']}"
            else:
                raise Exception("Failed to find that file")
        else:
            raise Exception("Error from Mythic trying to get file: " + str(file_resp.error))

        return task

    async def process_response(self, response: AgentResponse):
        pass

