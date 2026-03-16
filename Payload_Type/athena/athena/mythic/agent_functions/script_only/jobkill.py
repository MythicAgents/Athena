from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class JobKillArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="id",
                type=ParameterType.String,
                description="ID of the job to kill",
            )]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("id", self.command_line)
        else:
            raise ValueError("Missing Arguments")

class JobKillCommand(CommandBase):
    cmd = "jobkill"
    needs_admin = False
    script_only = True
    depends_on = "jobs"
    plugin_libraries = []
    help_cmd = "jobkill [taskid]"
    description = "Tasks Athena to exit a long running job."
    version = 1
    author = "@checkymander"
    argument_class = JobKillArguments
    attackmapping = ["T1059"]
    attributes = CommandAttributes()
    completion_functions = {
        "command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="jobs",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "kill",
                "id": taskData.args.get_arg("id")
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
