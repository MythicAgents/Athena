from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

class NNidhoggProtectRegistryValueArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="The registry key path to protect",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        )
                    ],
            ),
            CommandParameter(
                name="value",
                type=ParameterType.String,
                description="The registry key value to unprotect",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        )
                    ],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class NNidhoggProtectRegistryKeyCommand(CommandBase):
    cmd = "nidhogg-protectregistryvalue"
    needs_admin = False
    help_cmd = """nidhogg-unprotectregistryvalue -path HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\\test -value testvalue"""
    description = "Protects a process from being killed"
    version = 1
    script_only = True
    supported_ui_features = []
    author = "@idov31"
    argument_class = NNidhoggProtectRegistryValueArguments
    attackmapping = []
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
        builtin=False,
        load_only=True
    )
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )

        resp = await MythicRPC().execute("create_subtask_group", tasks=[
            {"command": "nidhogg", "params": {"command":"protectregistryvalue", "path":taskData.args.get_arg("path"), "value":taskData.args.get_arg("value")}},
            ], 
            subtask_group_name = "nidhogg", parent_task_id=taskData.Task.ID)

        # We did it!
        return response

    async def process_response(self, response: AgentResponse):
        pass