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
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="command", cli_name="command", display_name="Command to Load", type=ParameterType.ChooseOne,
                choices_are_all_commands=True,
                description="Load Command",
                parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    group_name="Default"
                ),
                ParameterGroupInfo(
                    required=True,
                    group_name="Custom"
                )
                ]
            ),
            CommandParameter(
                name="commandFile",
                type=ParameterType.File,
                description="List of hosts in a newline separated file",
                parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    group_name="Custom"
                )]
            )
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
    attackmapping = ["T1129", "T1059.002", "T1620"]
    argument_class = LoadArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_go_tasking(self, taskData: MythicCommandBase.PTTaskMessageAllData) -> MythicCommandBase.PTTaskCreateTaskingMessageResponse:
        response = MythicCommandBase.PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
        )
        groupName = taskData.args.get_parameter_group_name()
        if groupName == "Custom":
            file = await SendMythicRPCFileGetContent(MythicRPCFileGetContentMessage(taskData.args.get_arg("commandFile")))
            
            if file.Success:
                file_contents = base64.b64encode(file.Content)
                taskData.args.add_arg("asm", file_contents.decode("utf-8"), parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    group_name="Custom"
                )])
            else:
                await message_utilities.send_agent_message("Failed to get file contents: " + file.Error, taskData.Task)
                raise Exception("Failed to get file contents: " + file.Error)
            
            return response

        command = taskData.args.get_arg('command')

        bof_commands = plugin_utilities.get_coff_commands()
        shellcode_commands = plugin_utilities.get_inject_shellcode_commands()
        ds_commands = plugin_utilities.get_ds_commands()
        nidhogg_commands = plugin_utilities.get_nidhogg_commands()

        if command in bof_commands:
            await message_utilities.send_agent_message("Please load coff to enable this command", taskData.Task)
            raise Exception("Please load coff to enable this command")
        elif command in shellcode_commands:
            await message_utilities.send_agent_message("Please load inject-shellcode to enable this command", taskData.Task)
            raise Exception("Please load inject-shellcode to enable this command")
        elif command in ds_commands:
            await message_utilities.send_agent_message("Please load ds to enable this command", taskData.Task)
            raise Exception("Please load ds to enable this command")
        elif command in nidhogg_commands:
            await message_utilities.send_agent_message("Please load nidhogg to enable this command", taskData.Task)
            raise Exception("Please load nidhogg to enable this command")         
        
        command_checks = {
            "coff": plugin_utilities.get_coff_commands,
            "inject-shellcode": plugin_utilities.get_inject_shellcode_commands,
            "ds": plugin_utilities.get_ds_commands,
            "nidhogg" : plugin_utilities.get_nidhogg_commands,
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
            "screenshot": [{"libraryname": "System.Drawing.Common.dll", "target": "plugin"}],
            # Add more commands as needed
        }

        command_plugins = {
            "coff": bof_commands,
            "ds": ds_commands,
            "inject-shellcode": shellcode_commands,
            "nidhogg": nidhogg_commands,
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
        print(dllFile)
        print(dllFile2)
        # Try OS dependant first  
        if not os.path.isfile(dllFile2):
            print(f"Failed " + dllFile2)
            # Fallback to generic
            if not os.path.isfile(dllFile):
                print(f"Failed " + dllFile)
                raise Exception("Please wait for plugins to finish compiling.")
            else:
                print(f"Found " + dllFile)
                with open(dllFile, 'rb') as file:
                    dllBytes = file.read()
        else:
            print("Found " + dllFile2)
            with open(dllFile2, 'rb') as file:
                    dllBytes = file.read()

        encodedBytes = base64.b64encode(dllBytes)
        taskData.args.add_arg("asm", encodedBytes.decode(), parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    group_name="Default"
                )])
        
        return response
    
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
