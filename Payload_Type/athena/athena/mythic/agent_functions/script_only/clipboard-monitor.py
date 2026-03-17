from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class ClipboardMonitorArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="duration", cli_name="duration",
                display_name="Duration (seconds)",
                type=ParameterType.Number,
                description="How long to monitor clipboard",
                default_value=60,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False, group_name="Default",
                        ui_position=0)
                ]
            ),
            CommandParameter(
                name="interval", cli_name="interval",
                display_name="Poll Interval (seconds)",
                type=ParameterType.Number,
                description="How often to check clipboard",
                default_value=2,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False, group_name="Default",
                        ui_position=1)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class ClipboardMonitorCommand(CommandBase):
    cmd = "clipboard-monitor"
    needs_admin = False
    script_only = True
    depends_on = "clipboard"
    plugin_libraries = []
    help_cmd = "clipboard-monitor -duration 60 -interval 2"
    description = (
        "Monitor clipboard for changes "
        "(Windows only, long-running job)"
    )
    version = 1
    author = "@checkymander"
    argument_class = ClipboardMonitorArguments
    attackmapping = ["T1115"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )
    completion_functions = {
        "command_callback": default_completion_callback
    }

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        duration = taskData.args.get_arg("duration")
        interval = taskData.args.get_arg("interval")
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="clipboard",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "monitor",
                "duration": duration,
                "interval": interval
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = (
            f"{duration}s every {interval}s"
        )
        return response

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
