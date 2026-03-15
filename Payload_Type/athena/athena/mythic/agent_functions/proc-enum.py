from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class ProcEnumArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["proc-enum", "named-pipes"],
                default_value="proc-enum",
                description="Enumeration mode",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class ProcEnumCommand(CommandBase):
    cmd = "proc-enum"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "proc-enum -action proc-enum"
    description = "Enhanced process enumeration and named pipe listing"
    version = 1
    author = "@checkymander"
    argument_class = ProcEnumArguments
    attackmapping = ["T1057"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = taskData.args.get_arg("action")
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
