from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class KerberosArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["klist", "purge"],
                default_value="klist",
                description="Kerberos ticket operation",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class KerberosCommand(CommandBase):
    cmd = "kerberos"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "kerberos -action klist"
    description = "Kerberos ticket operations (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = KerberosArguments
    attackmapping = ["T1558"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
