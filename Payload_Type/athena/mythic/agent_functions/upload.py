from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *
import sys
import base64


class UploadArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(name="Destination",  type=ParameterType.String,
                              description="Path to write the file on the target. If empty, defaults to current working directory."),
            CommandParameter(name="File", type=ParameterType.File),
        ]

    async def parse_arguments(self):
        if len(self.command_line) == 0:
            raise Exception("Require arguments.")
        if self.command_line[0] != "{":
            raise Exception("Require JSON blob, but got raw command line.")
        self.load_args_from_json_string(self.command_line)
        pass


class UploadCommand(CommandBase):
    cmd = "upload"
    needs_admin = False
    help_cmd = "upload"
    description = "Upload a file from the mythic server to the remote host."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    supported_ui_features = ["file_browser:upload"]
    is_remove_file = False
    is_upload_file = True
    author = "@checkymander, @djhonstein"
    argument_class = UploadArguments
    attackmapping = ["T1132", "T1030", "T1105"]
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )
    
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        file_resp = await MythicRPC().execute(
            "get_file",
            file_id=task.args.get_arg("file"),
            task_id=task.id,
            get_contents=False)
        if file_resp.status == MythicRPCStatus.Success:
            original_file_name = file_resp.response[0]["filename"]
        else:
            raise Exception("Failed to fetch uploaded file from Mythic (ID: {})".format(task.args.get_arg("file")))
        
        task.args.add_arg("file_name", original_file_name)
        
        path = task.args.get_arg("path")
        disp_str = ""
        
        if path is not None and path != "":
                disp_str = "-File {} -Path {}".format(original_file_name, path)
        else:
            disp_str = "-File {}".format(original_file_name)
        task.display_params = disp_str
        return task

    async def process_response(self, response: AgentResponse):
        pass
