from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class DefaultsReadArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="domain", cli_name="domain",
                display_name="Domain",
                type=ParameterType.String,
                description="Defaults domain (e.g. com.apple.finder)",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
            CommandParameter(
                name="key", cli_name="key",
                display_name="Key",
                type=ParameterType.String,
                description="Defaults key to read (blank for all)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class DefaultsReadCommand(CommandBase):
    cmd = "defaults-read"
    needs_admin = False
    script_only = True
    depends_on = "jxa"
    plugin_libraries = []
    help_cmd = "defaults-read -domain com.apple.finder -key ShowHardDrivesOnDesktop"
    description = "Read macOS defaults (preferences) via JXA (macOS only)"
    version = 1
    author = "@checkymander"
    argument_class = DefaultsReadArguments
    attackmapping = ["T1082"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.MacOS],
    )
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        domain = taskData.args.get_arg("domain")
        key = taskData.args.get_arg("key")
        if key:
            jxa_code = f"$.NSUserDefaults.alloc.initWithSuiteName('{domain}').objectForKey('{key}')"
        else:
            jxa_code = f"JSON.stringify(ObjC.deepUnwrap($.NSUserDefaults.alloc.initWithSuiteName('{domain}').dictionaryRepresentation))"
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="jxa",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"code": jxa_code})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
