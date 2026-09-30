from mythic_container.MythicCommandBase import *  # import the basics
from mythic_container import *
from mythic_container.MythicRPC import *
from .athena_utils.mythicrpc_utilities import *
import os
from pathlib import Path
import re


def contained_dll_path(directory, dll_name):
    root = Path(directory).resolve()
    candidate = (root / dll_name).resolve()
    try:
        candidate.relative_to(root)
    except ValueError as error:
        raise ValueError("Library path is not contained in the advertised bin directory") from error
    return candidate


def select_advertised_dll(dll_name, platform_directory, common_directory, choices):
    if (
        not isinstance(dll_name, str)
        or not dll_name.lower().endswith(".dll")
        or Path(dll_name).name != dll_name
        or "/" in dll_name
        or "\\" in dll_name
        or dll_name not in choices
    ):
        raise ValueError("Library must be a basename-only DLL from the advertised choices")
    for directory in (platform_directory, common_directory):
        candidate = contained_dll_path(directory, dll_name)
        if candidate.is_file():
            return candidate
    raise ValueError("Failed to find the advertised DLL file")


# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class LoadAssemblyArguments(TaskArguments):
    agent_code_path = Path(".") / "athena" / "agent_code"

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
        callback_message = MythicRPCCallbackSearchMessage(
            AgentCallbackID=inputMsg.Callback
        )
        callback = await SendMythicRPCCallbackSearch(callback_message)
        if not callback.Success:
            return PTRPCDynamicQueryFunctionMessageResponse(
                Success=False,
                Error=callback.Error or "Callback lookup RPC failed",
                Choices=[],
            )
        if not callback.Results:
            return PTRPCDynamicQueryFunctionMessageResponse(
                Success=False,
                Error="Callback lookup returned no callback",
                Choices=[],
            )
        os_name = self.detect_os(callback.Results[0].Os)
        if os_name == "unknown":
            return PTRPCDynamicQueryFunctionMessageResponse(
                Success=False,
                Error="Unsupported callback OS: {}".format(callback.Results[0].Os),
                Choices=[],
            )
        bin_path = Path(self.agent_code_path) / "bin"
        choices = self.find_dll_files(bin_path / os_name)
        choices.extend(self.find_dll_files(bin_path / "common"))
        return PTRPCDynamicQueryFunctionMessageResponse(
            Success=True,
            Error="",
            Choices=sorted(set(choices)),
        )


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
        if not os.path.isdir(directory):
            return []
        choices = []
        for filename in os.listdir(directory):
            if not filename.lower().endswith(".dll"):
                continue
            try:
                candidate = contained_dll_path(directory, filename)
            except ValueError:
                continue
            if candidate.is_file():
                choices.append(filename)
        return choices

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
            os_paths = {
                "windows": Path(self.agent_code_path) / "bin" / "windows",
                "linux": Path(self.agent_code_path) / "bin" / "linux",
                "macos": Path(self.agent_code_path) / "bin" / "macos",
            }
            platform_directory = os_paths.get(taskData.Payload.OS.lower())

            if not platform_directory:
                raise Exception(f"This OS is not supported: {taskData.Payload.OS}")
            common_directory = Path(self.agent_code_path) / "bin" / "common"
            choices = set(LoadAssemblyArguments("").find_dll_files(platform_directory))
            choices.update(LoadAssemblyArguments("").find_dll_files(common_directory))
            dll_file_path = select_advertised_dll(
                dll_name, platform_directory, common_directory, choices
            )
            with dll_file_path.open("rb") as dll_file:
                dll_bytes = dll_file.read()

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


