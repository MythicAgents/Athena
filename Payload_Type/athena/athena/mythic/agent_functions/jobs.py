from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

class JobsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["list", "kill"],
                default_value="list",
                description="Action to perform on jobs",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True, group_name="Default")
                ]
            ),
            CommandParameter(
                name="id", cli_name="id",
                display_name="Task ID",
                type=ParameterType.String,
                default_value="",
                description="ID of the job to kill",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


class JobsCommand(CommandBase):
    cmd = "jobs"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "jobs -action list"
    description = "List active jobs or kill a running job by task ID."
    version = 2
    author = "@checkymander"
    argument_class = JobsArguments
    browser_script = BrowserScript(
        script_name="jobs", author="@checkymander")
    attackmapping = []
    attributes = CommandAttributes(
        load_only=False,
        builtin=False
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
