from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from Payload_Type.athena.athena.mythic.agent_functions.athena_messages import message_converter


class SleepArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="sleep",
                type=ParameterType.String,
                description="How long to sleep in between communications.",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            CommandParameter(
                name="jitter",
                type=ParameterType.String,
                description="The percentage to stagger the sleep by.",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("sleep", self.command_line.split()[0])
                self.add_arg("jitter", self.command_line.split()[1])


class SleepCommand(CommandBase):
    cmd = "sleep"
    needs_admin = False
    help_cmd = "sleep [seconds] [jitter]"
    description = "Change the implant's sleep interval."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = SleepArguments
    #attackmapping = ["T1029"]
    attackmapping = []
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        user_output = response["message"]
        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))
        return resp