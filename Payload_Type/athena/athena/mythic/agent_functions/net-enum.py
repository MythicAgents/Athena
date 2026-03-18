from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class NetEnumArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["ping", "traceroute", "ifconfig", "netstat", "arp", "test-port"],
                default_value="ping",
                description="Network enumeration action",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
            # ping / traceroute params
            CommandParameter(
                name="host", cli_name="host",
                display_name="Host",
                type=ParameterType.String,
                default_value="",
                description="Target hostname or IP (ping, traceroute)",
                parameter_group_info=[ParameterGroupInfo(required=False, group_name="Default")]
            ),
            CommandParameter(
                name="count", cli_name="count",
                display_name="Count",
                type=ParameterType.Number,
                default_value=4,
                description="Number of ping packets",
                parameter_group_info=[ParameterGroupInfo(required=False, group_name="Default")]
            ),
            CommandParameter(
                name="max_ttl", cli_name="max_ttl",
                display_name="Max TTL",
                type=ParameterType.Number,
                default_value=30,
                description="Max hops for traceroute",
                parameter_group_info=[ParameterGroupInfo(required=False, group_name="Default")]
            ),
            CommandParameter(
                name="timeout", cli_name="timeout",
                display_name="Timeout (ms)",
                type=ParameterType.Number,
                default_value=1000,
                description="Timeout in ms (ping/traceroute) or seconds (arp)",
                parameter_group_info=[ParameterGroupInfo(required=False, group_name="Default")]
            ),
            # arp params
            CommandParameter(
                name="cidr", cli_name="cidr",
                display_name="CIDR",
                type=ParameterType.String,
                default_value="",
                description="CIDR range to ARP scan",
                parameter_group_info=[ParameterGroupInfo(required=False, group_name="Default")]
            ),
            # test-port params
            CommandParameter(
                name="hosts", cli_name="hosts",
                display_name="Hosts",
                type=ParameterType.String,
                default_value="",
                description="Comma-separated hosts for port scan",
                parameter_group_info=[ParameterGroupInfo(required=False, group_name="Default")]
            ),
            CommandParameter(
                name="ports", cli_name="ports",
                display_name="Ports",
                type=ParameterType.String,
                default_value="",
                description="Comma-separated ports to test",
                parameter_group_info=[ParameterGroupInfo(required=False, group_name="Default")]
            ),
            CommandParameter(
                name="targetlist", cli_name="targetlist",
                display_name="Target List (base64)",
                type=ParameterType.String,
                default_value="",
                description="Base64-encoded newline-separated host list",
                parameter_group_info=[ParameterGroupInfo(required=False, group_name="Default")]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class NetEnumCommand(CommandBase):
    cmd = "net-enum"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "net-enum -action ping"
    description = "Network enumeration (ping, traceroute, ifconfig, netstat, arp, port scan)"
    version = 1
    author = "@checkymander"
    argument_class = NetEnumArguments
    attackmapping = ["T1016", "T1018", "T1046", "T1049"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
