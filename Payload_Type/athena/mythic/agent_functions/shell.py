from mythic_payloadtype_container.MythicCommandBase import *
import json


class ShellArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="command",
                type=ParameterType.String,
                description="The shell command to execute",
                default_value="",
                parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    ui_position=0,
                )],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line.strip()) == 0:
            raise Exception("shell requires at least one command-line parameter.\n\tUsage: {}".format(ShellCommand.help_cmd))
        pass


class ShellCommand(CommandBase):
    cmd = "shell"
    needs_admin = False
    help_cmd = "shell [command] [arguments]"
    description = "Run a shell command which will translate to a process being spawned with command line: `cmd.exe /C [command]`"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = ShellArguments
    attackmapping = ["T1059", "T1059.004"]
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass
