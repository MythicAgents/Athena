from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class AdwsConnectArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="server", cli_name="server",
                display_name="Server",
                type=ParameterType.String,
                description="Domain controller hostname or IP",
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
                self.add_arg("server", self.command_line.strip())

class AdwsConnectCommand(CommandBase):
    cmd = "adws-connect"
    needs_admin = False
    script_only = True
    depends_on = "adws"
    plugin_libraries = []
    help_cmd = "adws-connect dc01.domain.com"
    description = "Connect to Active Directory Web Services on a domain controller"
    version = 1
    author = "@checkymander"
    argument_class = AdwsConnectArguments
    attackmapping = ["T1087.002"]
    attributes = CommandAttributes(supported_os=[SupportedOS.Windows])
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="adws",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "connect",
                "server": taskData.args.get_arg("server")
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
