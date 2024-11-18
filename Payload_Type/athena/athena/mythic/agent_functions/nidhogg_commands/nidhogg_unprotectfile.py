from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.bof_utilities import *

class NidhoggUnProtectFileArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="The Path to unprotect",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        )
                    ],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("path", self.command_line)
        else:
            raise ValueError("Missing arguments")

class NidhoggUnProtectFileCommand(CoffCommandBase):
    cmd = "nidhogg-unprotectfile"
    needs_admin = False
    help_cmd = """nidhogg-unprotectfile C:\\Path\\to\\myfile.txt"""
    description = "Unprotects a protected file"
    version = 1
    script_only = True
    supported_ui_features = []
    author = "@idov31"
    argument_class = NidhoggUnProtectFileArguments
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

        subtask = await SendMythicRPCTaskCreateSubtask(MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID, 
            CommandName="nidhogg",
            SubtaskCallbackFunction="coff_completion_callback",
            Params=json.dumps({
                "command": "unprotectfile",
                "path": taskData.args.get_arg("path")
            }),
            Token=taskData.Task.TokenID,
        ))

        # We did it!
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass