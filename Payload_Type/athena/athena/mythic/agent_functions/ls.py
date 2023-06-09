from mythic_container.MythicCommandBase import *
import json
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
                        required=True,
                        group_name="Default",
                        ui_position=0
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
                    ),
                ])
        ]


    async def strip_host_from_path(self, path):
            host = ""
            if path[0] == "\\" and path[1] == "\\":
                final = path.find("\\", 2)
                if final != -1:
                    host = path[2:final]
                    path = path[final+1:]
            return (host, path)

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            # We'll never enter this control flow
            if self.command_line[0] == '{':
                temp_json = json.loads(self.command_line)
                if "file" in temp_json.keys():
                    # we came from the file browser
                    host = ""
                    path = temp_json['path']
                    if 'file' in temp_json and temp_json['file'] != "":
                        path += "\\" + temp_json['file']
                    if 'host' in temp_json:
                        # this means we have tasking from the file browser rather than the popup UI
                        # the apfell agent doesn't currently have the ability to do _remote_ listings, so we ignore it
                        host = temp_json['host']

                    self.add_arg("host", host)
                    self.add_arg("path", path)
                    self.add_arg("file_browser", "true")
                else:
                    self.load_args_from_json_string(self.command_line)
                    if ":" in self.get_arg("host"):
                        if self.get_arg("path") is None:
                            self.add_arg("path", self.get_arg("host"))
                        else:
                            self.add_arg("path", self.get_arg("host") + " " + self.get_arg("path"))
                        self.remove_arg("host")
            else:
                args = await self.strip_host_from_path(self.command_line)
                self.add_arg("host", args[0])
                self.add_arg("path", args[1])
                self.add_arg("file_browser", "true")
        else:
            self.add_arg("host", "")
            self.add_arg("path", self.command_line)
            self.add_arg("file_browser", "true")
        if self.get_arg("path") is None:
            self.add_arg("path", ".")
        if self.get_arg("host") is None or self.get_arg("host") == "":
            args = await self.strip_host_from_path(self.get_arg("path"))
            self.add_arg("host", args[0])
            self.add_arg("path", args[1])
        elif self.get_arg("path")[:2] == "\\\\":
            args = await self.strip_host_from_path(self.get_arg("path"))
            self.add_arg("host", args[0])
            self.add_arg("path", args[1])
        if self.get_arg("path") is not None and self.get_arg("path")[-1] == "\\":
            self.add_arg("path", self.get_arg("path")[:-1])



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
    #attackmapping = ["T1106", "T1083"]
    attackmapping = []
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
