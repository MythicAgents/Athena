from mythic_payloadtype_container.MythicCommandBase import *
import json


class PowerShellArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="command",
                type=ParameterType.String,
                description="Command to be executed",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
        ]
    async def parse_arguments(self):
        if len(self.command_line.strip()) == 0:
            raise Exception("powershell requires at least one command-line parameter.\n\tUsage: {}".format(PowerShellCommand.help_cmd))
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            self.args["path"].value = self.command_line
         



class PowerShellCommand(CommandBase):
    cmd = "powershell"
    needs_admin = False
    help_cmd = "powershell [command] [arguments]"
    description = "Run a powershell command in the agent process`"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@ascemama"
    argument_class = PowerShellArguments
    attackmapping = ["T1059", "T1059.004"]
    attributes = CommandAttributes(
        load_only=True,
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
