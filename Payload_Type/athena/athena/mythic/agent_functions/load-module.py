from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json
import base64
import os

class LoadModuleArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="module", cli_name="module", 
                display_name="Module to Load", 
                type=ParameterType.ChooseOne,
                description="Load Module",
                choices = ["ds", "ssh", "sftp"],
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0
                    )
                ]
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
                        required=False,
                        ui_position=1,
                        group_name="Default"
                    )
                ],
            ),
        ]

    async def parse_arguments(self):
        if self.command_line[0] == "{":
                tmpjson = json.loads(self.command_line)
                self.load_args_from_json_string(json.dumps(tmpjson))
        else:
                self.load_args_from_json_string(self.command_line)



class LoadModuleCommand(CommandBase):
    cmd = "load-module"
    needs_admin = False
    help_cmd = "load-module"
    description = """This loads required DLLs for a specific plugin:
    Supported Modules:
        - ds
        - ssh
        - sftp
    """
    version = 1
    script_only = True
    author = "@checkymander"
    parameters = []
    attackmapping = ["T1030", "T1129", "T1059.002", "T1620"]
    argument_class = LoadModuleArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        module = task.args.get_arg('module').lower()

        if(module == "ds"):
            resp = await MythicRPC().execute("create_subtask_group", tasks=[
                {"command": "load-assembly", "params": {"libraryname":"System.DirectoryServices.Protocols.dll", "target":task.args.get_arg('target').lower()}},
                {"command": "load", "params" : {"command":"ds"}}
                ], 
                subtask_group_name = "ds", group_callback_function=self.load_completed.__name__, parent_task_id=task.id)
        elif(module == "ssh"):
            resp = await MythicRPC().execute("create_subtask_group", tasks=[
                {"command": "load-assembly", "params" : {"libraryname":"Renci.SshNet.dll", "target":task.args.get_arg('target').lower()}},
                {"command": "load-assembly", "params" : {"libraryname":"SshNet.Security.Cryptography.dll", "target":task.args.get_arg('target').lower()}},
                {"command": "load", "params" : {"command":"ssh"}}
                ],
                subtask_group_name = "ssh", group_callback_function=self.load_completed.__name__, parent_task_id=task.id)
        elif(module == "sftp"):
            resp = await MythicRPC().execute("create_subtask_group", tasks=[
                {"command": "load-assembly", "params" : {"libraryname":"Renci.SshNet.dll", "target":task.args.get_arg('target').lower()}},
                {"command": "load-assembly", "params" : {"libraryname":"SshNet.Security.Cryptography.dll", "target":task.args.get_arg('target').lower()}},
                {"command": "load", "params" : {"command":"sftp"}}
                ],
                subtask_group_name = "sftp", group_callback_function=self.load_completed.__name__, parent_task_id=task.id)


        return task

    async def load_completed(self, task: MythicTask, subtask: dict = None, subtask_group_name: str = None) -> MythicTask:       
        resp = await MythicRPC().execute("create_output", task_id=task.id,
                                    output="Module Loaded!"
                                    )
        task.status = MythicStatus.Completed
        return task

    async def process_response(self, response: AgentResponse):
        pass

    async def get_commands(self, response: AgentResponse):
        pass

