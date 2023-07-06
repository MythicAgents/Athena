from mythic_container.MythicCommandBase import *  # import the basics
from mythic_container import *
import json  # import any other code you might need
import os
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
from os import listdir
from os.path import isfile, join, exists

from .athena_utils import message_converter

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
                type=ParameterType.String,
                #type=ParameterType.ChooseOne,
                #dynamic_query_function=self.get_libraries,
                #dynamic_query_function=Callable[[PTRPCDynamicQueryFunctionMessage], Awaitable[PTRPCDynamicQueryFunctionMessageResponse]] = None,
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

    async def get_libraries(self, callback: PTRPCDynamicQueryFunctionMessage) -> PTRPCDynamicQueryFunctionMessageResponse:
        file_names = []

    #async def get_libraries(self, callback: dict) -> [str]:
        # Get a directory listing based on the current OS Version
        # file_names = []
        # #if callback["payload"]["os"] == "Windows":
        # if callback.payload.os == "Windows":
        #     mypath = os.path.join("/","Mythic","agent_code", "AthenaPlugins", "bin", "windows")
        # elif callback.payload.os == "Linux":
        #     mypath = os.path.join("/","Mythic","agent_code", "AthenaPlugins", "bin", "linux")
        # elif callback.payload.os == "macOS":
        #     mypath = os.path.join("/","Mythic","agent_code", "AthenaPlugins", "bin", "macos")
        # else:
        #     file_names.append("No Supported Libraries")
        #     return file_names

        # file_names = [f for f in listdir(mypath) if isfile(join(mypath, f))]
        # file_names.remove(".keep")
        # mycommonpath = os.path.join("/","Mythic","agent_code", "AthenaPlugins", "bin", "common")
        # file_names += [f for f in listdir(mycommonpath) if isfile(join(mycommonpath, f))]
        # file_names.remove(".keep")
        # return file_names

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


    async def create_go_tasking(self, taskData: MythicCommandBase.PTTaskMessageAllData) -> MythicCommandBase.PTTaskCreateTaskingMessageResponse:
        response = MythicCommandBase.PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            #CompletionFunctionName="functionName"
        )

        groupName = taskData.Task.ParameterGroupName

        if groupName == "InternalLib":
            dllName = taskData.args.get_arg("libraryname")
            commonDll = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", "common", f"{dllName}")

            # Using an included library
            if taskData.Payload.OS.lower() == "windows":
                dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", "windows",
                                        f"{dllName}")
            elif taskData.Payload.OS.lower() == "linux":
                dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", "linux",
                                        f"{dllName}")
            elif taskData.Payload.OS.lower() == "macos":
                dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", "macos",
                                        f"{dllName}")
            else:
                raise Exception("This OS is not supported: " + taskData.Payload.OS)
            
            if(exists(dllFile)): #platform specficic
                dllBytes = open(dllFile, 'rb').read()
                encodedBytes = base64.b64encode(dllBytes)
            elif(exists(commonDll)):
                dllBytes = open(commonDll, 'rb').read()
                encodedBytes = base64.b64encode(dllBytes)
            else:
                raise Exception("Failed to find that file")

            print(groupName)
            # taskData.args.add_arg("asm", encodedBytes.decode(),
            #                      parameter_group_info=[ParameterGroupInfo(group_name="InternalLib")])
            taskData.args.add_arg("asm", encodedBytes.decode(),
                                parameter_group_info=[ParameterGroupInfo(group_name="InternalLib")])

            # taskData.args.add_arg("asm", encodedBytes.decode())
            print(taskData.args.get_arg("asm"))

            response.DisplayParams = f"{dllName}"
        else:
            fData = FileData()
            fData.AgentFileId = taskData.args.get_arg("library")
            file = await SendMythicRPCFileGetContent(fData)
            
            if file.Success:
                file_contents = base64.b64encode(file.Content)
                taskData.args.add_arg("asm", file_contents.decode("utf-8"))
            else:
                raise Exception("Failed to get file contents: " + file.Error)

            response.DisplayParams = f"{dllName}"
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp


