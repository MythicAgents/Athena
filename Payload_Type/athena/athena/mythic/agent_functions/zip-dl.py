from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from .athena_utils import message_converter
import json


class ZipDlArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="source",
                type=ParameterType.String,
                description="Source file to copy.",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Source will copy to this location",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            # CommandParameter(
            #     name="write",
            #     type=ParameterType.Boolean,
            #     description="Write file to disk before downloading",
            #     parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            # ),
            CommandParameter(
                name="force",
                type=ParameterType.Boolean,
                description="Force in memory storage of large zip files",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
        ]

    def check_string_array(self, array: list[str], substring: str):
        for string in array:
            if substring in string.lower():
                return True
        return False
    
    async def parse_arguments(self):
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            cmds = self.split_commandline()
            if len(cmds) > 1:
                if not self.check_string_array(cmds, "-force"):
                    self.add_arg("write", True)
            #This probably breaks in an event where -force is specified. Need to check
            self.add_arg("source", cmds[0])
            self.add_arg("destination", cmds[1])


class ZipDlCommand(CommandBase):
    cmd = "zip-dl"
    needs_admin = False
    help_cmd = """
Download a directory as a zip file in memory that's less than 1GB:
zip-dl C:\\Users\\checkymander\\secretstuff

Download a large directory as a zip file writing to disk first:
zip-dl C:\\Users\\checkymander\\secretstuff\\ C:\\Temp\\stage.zip

Download a directory as a zip file in memory that's larger than 1GB:
zip-dl C:\\Users\\checkymander\\secretstuff\\ -force=true

"""
    description = "Zip a directory and download it to Mythic"
    version = 1
    author = "@checkymander"
    argument_class = ZipDlArguments
    attackmapping = ["T1570"]
    attributes = CommandAttributes(
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