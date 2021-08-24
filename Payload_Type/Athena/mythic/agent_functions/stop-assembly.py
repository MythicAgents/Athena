from mythic_payloadtype_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_payloadtype_container.MythicRPC import *

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class StopAssemblyArguments(TaskArguments):
    def __init__(self, command_line):
        super().__init__(command_line)
        # this is the part where you'd add in your additional tasking parameters

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class ExecuteAssemblyCommand(CommandBase):
    cmd = "stop-assembly"
    needs_admin = False
    help_cmd = "stop-assembly"
    description = "Stop the currently running assembly"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = True
    is_remove_file = False
    is_upload_file = False
    author = ""
    argument_class = StopAssemblyArguments
    attackmapping = []
    browser_script = None
    browser_script = None

    # this function is called after all of your arguments have been parsed and validated that each "required" parameter has a non-None value
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass

