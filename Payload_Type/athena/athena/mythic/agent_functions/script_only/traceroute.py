from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class TracerouteArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="host", cli_name="host",
                display_name="Host",
                type=ParameterType.String,
                description="Target hostname or IP",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="max_ttl", cli_name="max_ttl",
                display_name="Max TTL",
                type=ParameterType.Number,
                description="Maximum number of hops",
                default_value=30,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="timeout", cli_name="timeout",
                display_name="Timeout (ms)",
                type=ParameterType.Number,
                description="Timeout per hop in milliseconds",
                default_value=1000,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("host", self.command_line.strip())

class TracerouteCommand(CommandBase):
    cmd = "traceroute"
    needs_admin = False
    script_only = True
    depends_on = "ping"
    plugin_libraries = []
    help_cmd = "traceroute -host 10.0.0.1"
    description = "Trace network route to host"
    version = 1
    author = "@checkymander"
    argument_class = TracerouteArguments
    attackmapping = ["T1018"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="ping",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "traceroute",
                "host": taskData.args.get_arg("host"),
                "max_ttl": taskData.args.get_arg("max_ttl"),
                "timeout": taskData.args.get_arg("timeout"),
                "count": 1,
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
