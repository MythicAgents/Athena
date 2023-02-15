from mythic_payloadtype_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_payloadtype_container.MythicRPC import *
import base64

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class CoffArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="coffFile",
                type=ParameterType.File,
                description="Upload COFF file to be executed (typically ends in .o)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=1,
                        )
                    ],
            ),
            CommandParameter(
                name="functionName",
                type=ParameterType.String,
                description="Name of entry function to execute in COFF",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=True,
                        default_value="go",
                        )
                    ],
            ),
            #CommandParameter(
            #    name="fileSize",
            #    type=ParameterType.String,
            #    description="Number of bytes the COFF file size is",
            #    parameter_group_info=[
            #        ParameterGroupInfo(
            #            ui_poition=3,
            #            required=True,
            #            )
            #        ],
            #),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                description="Arguments converted to bytes using beacon_compatibility.py",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
                        required=False,
                        default_value="go"
                        )
                    ],
            ),
            CommandParameter(
                name="timeout",
                type=ParameterType.String,
                description="Time to wait for the coff file to execute before killing it",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
                        required=False,
                        default_value="30"
                        )
                    ],
            ),
            # CommandParameter(
            #     name="argumentSize",
            #     type=ParameterType.String,
            #     description="Number of arguments packed into argumentData", 
            #     parameter_group_info=[
            #         ParameterGroupInfo(
            #             ui_position=5,
            #             required=True,
            #             default_value="0",
            #             )
            #         ],
            # )
        ]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class CoffCommand(CommandBase):
    cmd = "coff"
    needs_admin = False
    help_cmd = "coff"
    description = "Execute a COFF file in process. Leverages the Netitude RunOF project. argumentData can be generated using the beacon_generate.py script found in the TrustedSec COFFLoader GitHub repo. This command is not intended to be used directly, but can be."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@checkymander & @scottctaylor12"
    argument_class = CoffArguments
    attackmapping = []
    browser_script = None
    attributes = CommandAttributes(
        load_only=False,
        builtin=False
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        file_resp = await MythicRPC().execute("get_file",
                                              file_id=task.args.get_arg("coffFile"),
                                              task_id=task.id,
                                              get_contents=True)

        if file_resp.status == MythicRPCStatus.Success:
            if len(file_resp.response) > 0:
                decoded_buffer = base64.b64decode(file_resp.response[0]["contents"])
                task.args.add_arg("fileSize", f"{len(decoded_buffer)}")
                task.args.add_arg("asm", file_resp.response[0]["contents"])
                task.display_params = f"{file_resp.response[0]['filename']}"
            else:
                raise Exception("Failed to find that file")
        else:
            raise Exception("Error from Mythic trying to get file: " + str(file_resp.error))

        return task

    async def process_response(self, response: AgentResponse):
        pass
