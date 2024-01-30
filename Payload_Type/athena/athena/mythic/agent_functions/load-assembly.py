from mythic_container.MythicCommandBase import *  # import the basics
from mythic_container import *
import json  # import any other code you might need
import os
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
from os import listdir
from os.path import isfile, join, exists
import re

from .athena_utils import message_converter

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
            #CompletionFunctionName="functionName"
        )

        groupName = taskData.args.get_parameter_group_name()

        if groupName == "InternalLib":
            dllName = taskData.args.get_arg("libraryname")
            commonDll = os.path.join(self.agent_code_path, "bin", "common", f"{dllName}")

            # Using an included library
            if taskData.Payload.OS.lower() == "windows":
                dllFile = os.path.join(self.agent_code_path, "bin", "windows",
                                        f"{dllName}")
            elif taskData.Payload.OS.lower() == "linux":
                dllFile = os.path.join(self.agent_code_path, "bin", "linux",
                                        f"{dllName}")
            elif taskData.Payload.OS.lower() == "macos":
                dllFile = os.path.join(self.agent_code_path, "bin", "macos",
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
        
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp


