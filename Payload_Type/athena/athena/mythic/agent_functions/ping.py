from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class PingArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["ping", "traceroute"],
                default_value="ping",
                description="Network probe mode",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="host", cli_name="host",
                display_name="Host",
                type=ParameterType.String,
                description="Target hostname or IP",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="count", cli_name="count",
                display_name="Count",
                type=ParameterType.Number,
                description="Number of echo requests",
                default_value=4,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
            CommandParameter(
                name="timeout", cli_name="timeout",
                display_name="Timeout (ms)",
                type=ParameterType.Number,
                description="Timeout per request in milliseconds",
                default_value=1000,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=3)
                ]
            ),
            CommandParameter(
                name="max_ttl", cli_name="max_ttl",
                display_name="Max TTL",
                type=ParameterType.Number,
                description="Maximum TTL (traceroute hop limit)",
                default_value=30,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=4)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("host", self.command_line.strip())

class PingCommand(CommandBase):
    cmd = "ping"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "ping -host 10.0.0.1 -count 4"
    description = "ICMP ping and traceroute"
    version = 1
    author = "@checkymander"
    argument_class = PingArguments
    attackmapping = ["T1018"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = f"{taskData.args.get_arg('action')} {taskData.args.get_arg('host')}"
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
