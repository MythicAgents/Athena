from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class JobKillArguments(TaskArguments):

    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [            
            CommandParameter(
                name="id",
                cli_name="id",
                display_name="Id",
                description="The task to kill",
                type=ParameterType.String,
            )]

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
        builtin=False
    )
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response
    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
