from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class EventLogArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["query", "list", "etw-control"],
                default_value="list",
                description="Event log operation",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="log_name", cli_name="log_name",
                display_name="Log Name",
                type=ParameterType.String,
                description="Event log name (e.g. Application, Security, System)",
                default_value="Application",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="count", cli_name="count",
                display_name="Count",
                type=ParameterType.Number,
                description="Number of entries to return",
                default_value=10,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class EventLogCommand(CommandBase):
    cmd = "event-log"
    needs_admin = False
    depends_on = None
    plugin_libraries = ["System.Diagnostics.EventLog.dll"]
    help_cmd = "event-log -action query -log_name Security -count 20"
    description = "Query Windows Event Logs (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = EventLogArguments
    attackmapping = ["T1654"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = f"{taskData.args.get_arg('action')} {taskData.args.get_arg('log_name')}"
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
