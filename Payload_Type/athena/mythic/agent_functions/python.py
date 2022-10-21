from mythic_payloadtype_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
from mythic_payloadtype_container.MythicRPC import *

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class PythonArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="pyfile",
                type=ParameterType.File,
                description="Python File to execute",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],)]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class PythonCommand(CommandBase):
    cmd = "python"
    needs_admin = False
    help_cmd = "python"
    description = "Load python files using standard lib by utlising ironpython, You will need a to wrap your Code in function called Execute()"
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = ""
    argument_class = PythonArguments
    attackmapping = []
    browser_script = None
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass