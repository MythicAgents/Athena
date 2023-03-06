from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class Shellcoderguments(TaskArguments):
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
                name="output",
                description="Return output from shellcode buffer",
                type=ParameterType.Boolean,
                default_value=False,
                parameter_group_info=[],
            ),
        ]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class ShellcodeCommand(CommandBase):
    cmd = "shellcode"
    needs_admin = False
    help_cmd = "shellcode"
    description = "Load a buffer into the process and execute it"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = ""
    argument_class = Shellcoderguments
    attackmapping = []
    browser_script = None
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        file_resp = await MythicRPC().execute("get_file",
                                              file_id=task.args.get_arg("file"),
                                              task_id=task.id,
                                              get_contents=True)
        if file_resp.status == MythicRPCStatus.Success:
            if len(file_resp.response) > 0:
                task.args.add_arg("buffer", file_resp.response[0]["contents"])
                task.display_params = f"{file_resp.response[0]['filename']}"
            else:
                raise Exception("Failed to find that file")
        else:
            raise Exception("Error from Mythic trying to get file: " + str(file_resp.error))

        get_output = task.args.get_arg("output")

        if get_output:
            task.args.add_arg("get_output", "True")
        else:
            task.args.add_arg("get_output", "False")
            
        return task

    async def process_response(self, response: AgentResponse):
        pass

