from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import sys

from .athena_utils import message_converter

class UploadArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="file", cli_name="new-file", display_name="File to upload", type=ParameterType.File, description="Select new file to upload",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0
                    )
                ]
            ),
            CommandParameter(
                name="filename", cli_name="registered-filename", display_name="Filename within Mythic", description="Supply existing filename in Mythic to upload",
                type=ParameterType.ChooseOne,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="specify already uploaded file by name"
                    )
                ]
            ),
            CommandParameter(
                name="remote_path",
                cli_name="remote_path",
                display_name="Upload path (with filename)",
                type=ParameterType.String,
                description="Provide the path where the file will go",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    ),
                    ParameterGroupInfo(
                        required=True,
                        group_name="specify already uploaded file by name",
                        ui_position=1
                    )
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) == 0:
            raise Exception("Require arguments.")
        if self.command_line[0] != "{":
            raise Exception("Require JSON blob, but got raw command line.")
        self.load_args_from_json_string(self.command_line)
        remote_path = self.get_arg("remote_path")
        if remote_path != "" and remote_path != None:
            remote_path = remote_path.strip()
            if remote_path[0] == '"' and remote_path[-1] == '"':
                remote_path = remote_path[1:-1]
            elif remote_path[0] == "'" and remote_path[-1] == "'":
                remote_path = remote_path[1:-1]
            self.add_arg("remote_path", remote_path)
        pass


class UploadCommand(CommandBase):
    cmd = "upload"
    needs_admin = False
    help_cmd = "upload"
    description = (
        "Upload a file to the target machine by selecting a file from your computer. "
    )
    version = 1
    supported_ui_features = ["file_browser:upload"]
    author = "@checkymander"
    #attackmapping = ["T1020", "T1030", "T1041", "T1105"]
    attackmapping = []
    argument_class = UploadArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        file_resp = await SendMythicRPCFileSearch(MythicRPCFileSearchMessage(
            TaskID=taskData.Task.ID,
            AgentFileID=taskData.args.get_arg("file")
        ))
        if file_resp.Success:
            original_file_name = file_resp.Files[0].Filename
        else:
            raise Exception("Failed to fetch uploaded file from Mythic (ID: {})".format(taskData.args.get_arg("file")))

        taskData.args.add_arg("file_name", original_file_name, type=ParameterType.String)
        host = taskData.args.get_arg("host")
        path = taskData.args.get_arg("remote_path")
        if path is not None and path != "":
            if host is not None and host != "":
                disp_str = "-File {} -Host {} -Path {}".format(original_file_name, host, path)
            else:
                disp_str = "-File {} -Path {}".format(original_file_name, path)
        else:
            disp_str = "-File {}".format(original_file_name)
        response.DisplayParams = disp_str
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp