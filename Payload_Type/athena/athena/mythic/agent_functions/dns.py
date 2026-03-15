from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class DnsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="hostname", cli_name="hostname",
                display_name="Hostname",
                type=ParameterType.String,
                description="Hostname to resolve",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="record_type", cli_name="record_type",
                display_name="Record Type",
                type=ParameterType.ChooseOne,
                choices=["A", "AAAA", "CNAME", "PTR"],
                default_value="A",
                description="DNS record type to query",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=1)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hostname", self.command_line.strip())

class DnsCommand(CommandBase):
    cmd = "dns"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "dns -hostname example.com -record_type A"
    description = "DNS resolution (A, AAAA, CNAME, PTR)"
    version = 1
    author = "@checkymander"
    argument_class = DnsArguments
    attackmapping = ["T1018"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = f"{taskData.args.get_arg('record_type')} {taskData.args.get_arg('hostname')}"
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
