from mythic_payloadtype_container.MythicCommandBase import *
import json


class FarmerArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="port",
                type=ParameterType.String,
                description="The port to run on",
                parameter_group_info=[ParameterGroupInfo(
                        required=False,
                        ui_position=0,
                        group_name="Default"
                    ),
                ],
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
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    
    author = "@domchell, @checkymander"
    argument_class = FarmerArguments
    attackmapping = []
    attributes = CommandAttributes(
        load_only=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass