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
