from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class InjectShellcodeMacosArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="pid", cli_name="pid",
                display_name="Target PID",
                type=ParameterType.Number,
                description="Process ID to inject into",
                default_value=0,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="commandline", cli_name="commandline",
                display_name="Spawn Command",
                type=ParameterType.String,
                description="Command line for new process (if pid=0)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="asm", cli_name="asm",
                display_name="Shellcode (base64)",
                type=ParameterType.String,
                description="Base64-encoded shellcode",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=2)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class InjectShellcodeMacosCommand(CommandBase):
    cmd = "inject-shellcode-macos"
    needs_admin = True
    depends_on = None
    plugin_libraries = []
    help_cmd = "inject-shellcode-macos -pid 1234 -asm <base64>"
    description = "Inject shellcode into a process via Mach APIs (macOS only)"
    version = 1
    author = "@checkymander"
    argument_class = InjectShellcodeMacosArguments
    attackmapping = ["T1055"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.MacOS],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
