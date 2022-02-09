from mythic_payloadtype_container.MythicCommandBase import *
from mythic_payloadtype_container.MythicRPC import *
import json
import base64
import os

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
            CommandParameter(
                name="library",
                cli_name="library",
                display_name="Supported Library",
                description="Load a supported 3rd party library directly into the agent",
                type=ParameterType.ChooseOne,
                dynamic_query_function=self.get_libraries,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Load Supported Library"
                    )
                ]
            ),
        ]

    async def get_libraries(self, callback: dict) -> [str]:
        # Get a directory listing based on the current OS Version
        file_names = []

        if(task.callback.payload["os"] == "Windows"):
            file_names.append(f["WinTest1"])
            file_names.append(f["WinTest2"])
        elif(task.callback.payload["os"] == "Linux"):
            file_names.append(f["WinTest1"])
            file_names.append(f["WinTest2"])
        elif(task.callback.payload["os"] == "macOS"):
            file_names.append(f["WinTest1"])
            file_names.append(f["WinTest2"])
        else:
            return []
        return file_names

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                j = json.loads(self.command_line)
                self.set_arg("name", j["command"])
            else:
                self.add_arg("name", self.command_line)
                # self.load_args_from_json_string(self.command_line)


class LoadCommand(CommandBase):
    cmd = "load"
    needs_admin = False
    help_cmd = "load cmd"
    description = "This loads a new plugin into memory via the C2 channel."
    version = 1
    author = "@checkymander"
    parameters = []
    attackmapping = ["T1030", "T1129"]
    argument_class = LoadArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        dllFile = os.path.join(self.agent_code_path, "AthenaPlugins","bin", f"{task.args.get_arg('name')}.dll")
        dllBytes = open(dllFile, 'rb').read()
        encodedBytes = base64.b64encode(dllBytes)
        task.args.add_arg("assembly", encodedBytes.decode())
        return task

    async def process_response(self, response: AgentResponse):
        pass

    async def get_commands(self, response: AgentResponse):
        pass

