from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_utils import message_converter


class ConfigArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="sleep",
                type=ParameterType.Number,
                description="How long to sleep in between communications.",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    ui_position=0
                    )
                ],
            ),
            CommandParameter(
                name="jitter",
                type=ParameterType.Number,
                description="The percentage to stagger the sleep by.",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="inject",
                type=ParameterType.Number,
                description="Changes Injection technique.",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    ui_position=2
                    )
                ],
            ),
            CommandParameter(
                name="killdate",
                type=ParameterType.String,
                description="Killdate in the format MM/DD/YYYY.",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    ui_position=3
                    )
                ],
            ),
            CommandParameter(
                name="chunk_size",
                type=ParameterType.Number,
                description="Chunk size for file transfers in bytes 1mb = 1000000",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    ui_position=4
                    )
                ],
            ),
            CommandParameter(
                name="prettyOutput",
                type=ParameterType.ChooseOne,
                choices=["true", "false"],
                description="Choose whether to pretty print output or not.",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    ui_position=5
                    )
                ],
            ),
            CommandParameter(
                name="debug",
                type=ParameterType.ChooseOne,
                choices=["true", "false"],
                description="Whether to print debug messages with task output",
                parameter_group_info=[ParameterGroupInfo(
                    required=False,
                    ui_position=3
                    )
                ],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


class ConfigCommand(CommandBase):
    cmd = "config"
    needs_admin = False
    help_cmd = "config -sleep [sleep in seconds] -jitter [jitter in %] -killdate [MM/DD/YYYY] -chunk_size [chunk size in bytes] -inject [ID]"
    description = "Change the implant configuration options."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = ConfigArguments
    attackmapping = ["T1029"]
    attributes = CommandAttributes(
        load_only=False,
        builtin=False
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
