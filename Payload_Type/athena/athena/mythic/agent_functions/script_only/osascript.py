from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class OsascriptArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="script", cli_name="script",
                display_name="AppleScript",
                type=ParameterType.String,
                description="AppleScript code to execute",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("script", self.command_line)

class OsascriptCommand(CommandBase):
    cmd = "osascript"
    needs_admin = False
    script_only = True
    depends_on = "jxa"
    plugin_libraries = []
    help_cmd = "osascript -script \"tell application \\\"Finder\\\" to get name of every disk\""
    description = "Execute AppleScript via JXA bridge (macOS only)"
    version = 1
    author = "@checkymander"
    argument_class = OsascriptArguments
    attackmapping = ["T1059.002"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        script = taskData.args.get_arg("script")
        jxa_code = f"var app = Application.currentApplication(); app.includeStandardAdditions = true; app.doShellScript('osascript -e ' + JSON.stringify({json.dumps(script)}))"
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="jxa",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"code": jxa_code})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
