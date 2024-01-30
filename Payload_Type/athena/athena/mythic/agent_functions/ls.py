from mythic_container.MythicCommandBase import *
from pathlib import PurePath
import json
import os
import re
from mythic_container.MythicRPC import *
from .athena_utils import message_converter


class DirectoryListArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="Path of file or folder on the current system to list",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    ),
                ]
            ),
            CommandParameter(
                name="host",
                cli_name="Host",
                display_name="Host",
                type=ParameterType.String,
                description="Host to list files from.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=2
                    ),
                ])
        ]       

    def build_file_path(self, parsed_info):
        if parsed_info['host']:
            # If it's a UNC path
            file_path = f"\\\\{parsed_info['host']}\\{parsed_info['folder_path']}\\{parsed_info['file_name']}"
        else:
            # If it's a Windows or Linux path
            file_path = os.path.join(parsed_info['folder_path'], parsed_info['file_name'])

        return file_path

    def parse_file_path(self, file_path):
        # Check if the path is a UNC path
        unc_match = re.match(r'^\\\\([^\\]+)\\(.+)$', file_path)
        
        if unc_match:
            host = unc_match.group(1)
            folder_path = unc_match.group(2)
            file_name = None  # Set file_name to None if the path ends in a folder
            if folder_path:
                file_name = os.path.basename(folder_path)
                folder_path = os.path.dirname(folder_path)
        else:
            # Use os.path.normpath to handle both Windows and Linux paths
            normalized_path = os.path.normpath(file_path)
            # Split the path into folder path and file name
            folder_path, file_name = os.path.split(normalized_path)
            host = None

            # Check if the path ends in a folder
            if not file_name:
                file_name = None

            # Check if the original path used Unix-style separators
            if '/' in file_path:
                folder_path = folder_path.replace('\\', '/')

        return {
            'host': host,
            'folder_path': folder_path,
            'file_name': file_name
        }
    
    async def parse_arguments(self):
        if (len(self.raw_command_line) > 0):
            if(self.raw_command_line[0] == "{"):
                temp_json = json.loads(self.raw_command_line)
                if "file" in temp_json: # This means it likely came from the file 
                    self.load_args_from_json_string(self.raw_command_line)
            else:
                path_parts = self.parse_file_path(self.raw_command_line)
                combined_path = self.build_file_path({"host":"","folder_path":path_parts["folder_path"],"file_name":path_parts["file_name"]})
                self.add_arg("path", combined_path)
                self.add_arg("host", path_parts["host"])
                

class DirectoryListCommand(CommandBase):
    cmd = "ls"
    needs_admin = False
    help_cmd = "ls [/path/to/directory]"
    description = "Get a directory listing of the requested path, or the current one if none provided."
    version = 1
    supported_ui_features = ["file_browser:list"]
    author = "@checkymander"
    argument_class = DirectoryListArguments
    attackmapping = ["T1005", "T1083", "T1082"]
    browser_script = BrowserScript(script_name="ls", author="@tr41nwr3ck")
    attributes = CommandAttributes(
    )
    
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        host = taskData.args.get_arg("host")
        path = taskData.args.get_arg("path")
        if host:
            response.DisplayParams = "{} on {}".format(path, host)
        else:
            response.DisplayParams = path
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp