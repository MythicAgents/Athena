from mythic_container.MythicCommandBase import *  # import the basics
from mythic_container import *
from mythic_container.MythicRPC import *
from .athena_utils.mythicrpc_utilities import *
from os.path import exists
import os
import re


# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class LoadAssemblyArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
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
            CommandParameter(
                name="target",
                cli_name="target",
                display_name="Where to load the library",
                description="Load a supported 3rd party library directly into the agent",
                type=ParameterType.ChooseOne,
                choices=["external","plugin"],
                default_value = "plugin",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=1,
                        group_name="InternalLib"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=1,
                        group_name="Default"
                    )
                ],
            ),
            
        ]

    async def get_libraries(self, inputMsg: PTRPCDynamicQueryFunctionMessage) -> PTRPCDynamicQueryFunctionMessageResponse:
        file_names = []
        callbackSearchMessage = MythicRPCCallbackSearchMessage (AgentCallbackID=inputMsg.Callback)
        callback =  await SendMythicRPCCallbackSearch(callbackSearchMessage)

        if(callback.Error):
            return file_names
                
        osVersion = self.detect_os(callback.Results[0].Os)
        if  osVersion.lower() == "windows":
            file_names = self.find_dll_files(os.path.join("/","Mythic", "athena", "agent_code", "bin", "windows"))
        elif osVersion.lower() == "linux":
            file_names = self.find_dll_files(os.path.join("/","Mythic", "athena", "agent_code", "bin", "linux"))
        elif osVersion.lower() == "macos":
            file_names = self.find_dll_files(os.path.join("/","Mythic", "athena", "agent_code", "bin", "macos"))

        file_names = file_names + self.find_dll_files(os.path.join("/","Mythic", "athena", "agent_code", "bin", "common"))

        for name in file_names:
            print(name)

        return file_names


    def detect_os(self, version_string):
        version_string = version_string.lower()

        if re.search(r'windows', version_string):
            return 'windows'
        elif re.search(r'linux', version_string):
            return 'linux'
        elif re.search(r'mac|darwin', version_string):
            return 'macos'
        else:
            return 'unknown'

    def find_dll_files(self, directory):
        dll_files = []

        # Iterate over files in the directory
        for filename in os.listdir(directory):
            # Check if the file has a .dll extension
            if filename.lower().endswith('.dll'):
                # Add the DLL file name to the array
                dll_files.append(filename)

        return dll_files

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
    author = ""
    argument_class = LoadAssemblyArguments
    attackmapping = ["T1620"]
    browser_script = None
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )


    async def create_go_tasking(self, taskData: MythicCommandBase.PTTaskMessageAllData) -> MythicCommandBase.PTTaskCreateTaskingMessageResponse:
        response = MythicCommandBase.PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
        )

        group_name = taskData.args.get_parameter_group_name()

        if group_name == "InternalLib":
            dll_name = taskData.args.get_arg("libraryname")
            common_dll_path = os.path.join(self.agent_code_path, "bin", "common", dll_name)

            # Determine the platform-specific DLL path
            os_paths = {
                "windows": os.path.join(self.agent_code_path, "bin", "windows", dll_name),
                "linux": os.path.join(self.agent_code_path, "bin", "linux", dll_name),
                "macos": os.path.join(self.agent_code_path, "bin", "macos", dll_name),
            }
            dll_file_path = os_paths.get(taskData.Payload.OS.lower())

            if not dll_file_path:
                raise Exception(f"This OS is not supported: {taskData.Payload.OS}")

            # Read and encode the DLL file
            if exists(dll_file_path):  # Platform-specific DLL
                with open(dll_file_path, "rb") as dll_file:
                    dll_bytes = dll_file.read()
            elif exists(common_dll_path):  # Common DLL
                with open(common_dll_path, "rb") as dll_file:
                    dll_bytes = dll_file.read()
            else:
                raise Exception("Failed to find the specified DLL file.")

            encoded_bytes = base64.b64encode(dll_bytes).decode()

            # Add arguments for the DLL
            taskData.args.add_arg(
                "asm",
                encoded_bytes,
                parameter_group_info=[ParameterGroupInfo(group_name="InternalLib")],
            )
            response.DisplayParams = dll_name
        else:
            # Handle user-supplied library
            encoded_file_contents = await get_mythic_file(taskData.args.get_arg("library"))
            original_file_name = await get_mythic_file_name(taskData.args.get_arg("library"))
            taskData.args.add_arg("asm", encoded_file_contents)
            response.DisplayParams = original_file_name

        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass


