from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *
import base64


class LoadArguments(TaskArguments):
    def __init__(self, command_line):
        super().__init__(command_line)
        self.args = {}

    async def parse_arguments(self):
        if len(self.command_line) == 0:
            raise ValueError("Need to specify commands to load")
        pass


class LoadCommand(CommandBase):
    cmd = "load"
    needs_admin = False
    help_cmd = "load cmd1 cmd2 cmd3..."
    description = "This loads new functions into memory via the C2 channel."
    version = 1
    author = "@its_a_feature_"
    parameters = []
    attackmapping = ["T1030", "T1129"]
    argument_class = LoadArguments


#Todo update this to find the proper dll files
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        total_code = ""
        for cmd in task.args.command_line.split(" "):
            cmd = cmd.strip()
            try:
                code_path = self.agent_code_path / "{}.js".format(cmd)
                total_code += open(code_path, "r").read() + "\n"
            except Exception as e:
                raise Exception("Failed to find code for '{}'".format(cmd))
        resp = await MythicRPC().execute("create_file", task_id=task.id,
            file=base64.b64encode(total_code.encode()).decode()
        )
        if resp.status == MythicStatus.Success:
            task.args.add_arg("file_id", resp.response["agent_file_id"])
            task.args.add_arg("cmds", task.args.command_line)
        else:
            raise Exception("Failed to register file: " + resp.error)
        return task

    async def process_response(self, response: AgentResponse):
        pass

