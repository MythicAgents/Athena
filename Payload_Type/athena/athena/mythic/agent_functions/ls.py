from mythic_container.MythicCommandBase import *
import json
import os
import re
from mythic_container.MythicRPC import *
from .athena_utils import message_converter


class DirectoryListArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
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


    async def strip_host_from_path(self, path):
        host = ""
        if path[0] == "\\" and path[1] == "\\":
            final = path.find("\\", 2)
            if final != -1:
                host = path[2:final]
                path = path[final+1:]
        return (host, path)
    
    async def parse_arguments(self):
        if (len(self.command_line) > 0):
            if(self.command_line[0] == "{"):
                temp_json = json.loads(self.command_line)
                if "host" in temp_json: # This means it likely came from the file 
                    self.load_args_from_json_string(self.command_line)
                else: # this means it came from the UI and has been parsed by mythic to a json parameter with only `path` in it
                    print(self.command_line)
                    path_parts = self.parse_file_path(temp_json["path"])
                    self.add_arg("host", path_parts["host"])
                    self.add_arg("path", path_parts["folder_path"])
                    self.add_arg("file", path_parts["file_name"])
            else:
                path_parts = self.parse_file_path(self.command_line)
                self.add_arg("host", path_parts["host"])
                self.add_arg("path", path_parts["folder_path"])
                self.add_arg("file", path_parts["file_name"])
                

class DirectoryListCommand(CommandBase):
    cmd = "ls"
    needs_admin = False
    help_cmd = "ls [/path/to/directory]"
    description = "Get a directory listing of the requested path, or the current one if none provided."
    version = 1
    is_exit = False
    is_file_browse = True
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    supported_ui_features = ["file_browser:list"]
    author = "@checkymander"
    argument_class = DirectoryListArguments
    attackmapping = ["T1106", "T1083"]
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