from .athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class LaunchdEnumArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="scope", cli_name="scope",
                display_name="Scope",
                type=ParameterType.ChooseOne,
                choices=["user", "system", "all"],
                default_value="all",
                description="Search user agents, system daemons, or both",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class LaunchdEnumCommand(CommandBase):
    cmd = "launchd-enum"
    needs_admin = False
    script_only = True
    depends_on = "find"
    plugin_libraries = []
    help_cmd = "launchd-enum -scope all"
    description = "Enumerate LaunchAgents and LaunchDaemons (macOS only)"
    version = 1
    author = "@checkymander"
    argument_class = LaunchdEnumArguments
    attackmapping = ["T1543.004"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        scope = taskData.args.get_arg("scope")
        paths = []
        if scope in ("user", "all"):
            paths.append("~/Library/LaunchAgents")
        if scope in ("system", "all"):
            paths.extend([
                "/Library/LaunchAgents",
                "/Library/LaunchDaemons",
                "/System/Library/LaunchAgents",
                "/System/Library/LaunchDaemons",
            ])

        for path in paths:
            subtask = MythicRPCTaskCreateSubtaskMessage(
                taskData.Task.ID,
                CommandName="find",
                Token=taskData.Task.TokenID,
                SubtaskCallbackFunction="command_callback",
                Params=json.dumps({
                    "path": path,
                    "name": "*.plist",
                })
            )
            await SendMythicRPCTaskCreateSubtask(subtask)

        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
