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
                default_value=".",
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
        print("Checking Commandline")
        print(self.command_line)
        if len(self.command_line) > 0:
            if self.command_line[0] == '{':
                self.load_args_from_json_string(self.command_line)
            else:
                args_dict = self.parse_file_path(self.command_line)
                file_path_dict = {args_dict["folder_path"],args_dict["file_name"] }
                print(file_path_dict)
                self.add_arg("host", args_dict["host"])
                self.add_arg("path", self.build_file_path(file_path_dict))
                self.add_arg("1", args_dict["folder_path"])
                self.add_arg("2", args_dict["host"])
                self.add_arg("3", args_dict["file_name"])

    # async def parse_arguments(self):
    #     if len(self.command_line) > 0:
    #         # We'll never enter this control flow
    #         if self.command_line[0] == '{':
    #             temp_json = json.loads(self.command_line)
    #             if "file" in temp_json.keys():
    #                 # we came from the file browser
    #                 host = ""
    #                 path = temp_json['path']
    #                 if 'file' in temp_json and temp_json['file'] != "":
    #                     path += "\\" + temp_json['file']
    #                 if 'host' in temp_json:
    #                     # this means we have tasking from the file browser rather than the popup UI
    #                     host = temp_json['host']

    #                 self.add_arg("host", host)
    #                 self.add_arg("path", path)
    #                 self.add_arg("file_browser", "true")
    #             else:
    #                 self.load_args_from_json_string(self.command_line)
    #                 if self.get_arg("host") is not None and ":" in self.get_arg("host"):
    #                     if self.get_arg("path") is None:
    #                         self.add_arg("path", self.get_arg("host"))
    #                     else:
    #                         self.add_arg("path", self.get_arg("host") + " " + self.get_arg("path"))
    #                     self.remove_arg("host")
    #                 if self.get_arg("host") is not None and self.get_arg("path") is None:
    #                     self.add_arg("path", self.get_arg("host"))
    #                     self.set_arg("host", "")
    #         else:
    #             args = await self.strip_host_from_path(self.command_line)
    #             self.add_arg("host", args[0])
    #             self.add_arg("path", args[1])
    #             self.add_arg("file_browser", "true")
    #     else:
    #         self.add_arg("host", "")
    #         self.add_arg("path", self.command_line)
    #         self.add_arg("file_browser", "true")
    #     if self.get_arg("path") is None:
    #         self.add_arg("path", ".")
    #     if self.get_arg("host") is None or self.get_arg("host") == "":
    #         args = await self.strip_host_from_path(self.get_arg("path"))
    #         self.add_arg("host", args[0])
    #         self.add_arg("path", args[1])
    #     elif self.get_arg("path")[:2] == "\\\\":
    #         args = await self.strip_host_from_path(self.get_arg("path"))
    #         self.add_arg("host", args[0])
    #         self.add_arg("path", args[1])
    #     if self.get_arg("path") is not None and self.get_arg("path")[-1] == "\\":
    #         self.add_arg("path", self.get_arg("path")[:-1])
                

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