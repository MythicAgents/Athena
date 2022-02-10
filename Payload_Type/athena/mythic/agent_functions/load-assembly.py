from mythic_payloadtype_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
import os
# import the code for interacting with Files on the Mythic server
from mythic_payloadtype_container.MythicRPC import *
from os import listdir
from os.path import isfile, join

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class LoadAssemblyArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="library",
                type=ParameterType.File,
                description="Custom 3rd party library",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0
                    )
                ],
            ),
            CommandParameter(
                name="libraryname",
                cli_name="libraryname",
                display_name="Supported Library",
                description="Load a supported 3rd party library directly into the agent",
                type=ParameterType.ChooseOne,
                dynamic_query_function=self.get_libraries,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="InternalLib"
                    )
                ],
            ),
        ]

    async def get_libraries(self, callback: dict) -> [str]:
        # Get a directory listing based on the current OS Version
        file_names = []
        if callback["payload"]["os"] == "Windows":
            mypath = os.path.join("/","Mythic","agent_code", "AthenaPlugins", "bin", "windows")
        elif callback["payload"]["os"] == "Linux":
            mypath = os.path.join("/","Mythic","agent_code", "AthenaPlugins", "bin", "linux")
        elif callback["payload"]["os"] == "macOS":
            mypath = os.path.join("/","Mythic","agent_code", "AthenaPlugins", "bin", "macos")
        else:
            file_names.append("No Supported Libraries")
            return file_names

        file_names = [f for f in listdir(mypath) if isfile(join(mypath, f))]
        return file_names

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class LoadAssemblyCommand(CommandBase):
    cmd = "load-assembly"
    needs_admin = False
    help_cmd = "load-assembly"
    description = "Load an arbitrary .NET assembly into the AssemblyLoadContext via Assembly.Load."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = True
    is_remove_file = False
    is_upload_file = False
    author = ""
    argument_class = LoadAssemblyArguments
    attackmapping = []
    browser_script = None
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    # this function is called after all of your arguments have been parsed and validated that each "required" parameter has a non-None value
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        if task.args.get_parameter_group_name() == "InternalLib":
            if task.callback.payload["os"] == "Windows":
                dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", "windows",
                                       f"{task.args.get_arg('library')}")
            elif task.callback.payload["os"] == "Linux":
                dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", "linux",
                                       f"{task.args.get_arg('library')}")
            elif task.callback.payload["os"] == "macOS":
                dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", "macos",
                                       f"{task.args.get_arg('library')}")
            dllBytes = open(dllFile, 'rb').read()
            encodedBytes = base64.b64encode(dllBytes)
            task.args.add_arg("assembly", encodedBytes.decode())
        else:
            assembly_name = json.loads(task.original_params)["library"]
            assembly_bytes = task.args.get_arg("library")

        return task

    async def process_response(self, response: AgentResponse):
        pass


