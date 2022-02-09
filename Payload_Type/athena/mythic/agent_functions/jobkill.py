from mythic_payloadtype_container.MythicCommandBase import *
import json


class JobKillArguments(TaskArguments):

    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        if len(self.command_line.strip()) == 0:
            raise Exception("You must specify a task id for use with jobkill.\n\tUsage: {}".format(JobKillCommand.help_cmd))
        pass


class JobKillCommand(CommandBase):
    cmd = "jobkill"
    needs_admin = False
    help_cmd = "jobkill [taskid]"
    description = "Tasks Athena to exit a long running job."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = JobKillArguments
    attackmapping = ["T1059"]
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
