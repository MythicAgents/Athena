from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_utils import message_converter


class FarmerArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="port",
                type=ParameterType.Number,
                description="The port to run on",
                default_value = 7777,
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("port", self.command_line.split()[0])
        else:
            raise ValueError("Missing arguments")


class FarmerCommand(CommandBase):
    cmd = "farmer"
    needs_admin = False
    help_cmd = "farmer"
    description = "Farmer is a project for collecting NetNTLM hashes in a Windows domain."
    help_cmd = """
Farmer https://github.com/mdsecactivebreach/Farmer
    created by @domchell

Farmer is acts as a WebDAV server in order to catch NetNTLMv2 Authentication hashes from Windows clients.

The server will listen on the specified port and will respond to any WebDAV request with a 401 Unauthorized response. The server will then wait for the client to send the NTLMv2 authentication hash.
Usage: farmer [port]
     
    """
    version = 1
    author = "@domchell, @checkymander"
    argument_class = FarmerArguments
    attackmapping = ["T1187"]
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