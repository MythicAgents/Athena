from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json
import re
import os

class RmArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path",
                cli_name="Path",
                display_name="Directory of File",
                type=ParameterType.String,
                description="The full path of the file to remove on the specified host",
                parameter_group_info=[
                    ParameterGroupInfo(required=False),
                ],
            ),
            CommandParameter(
                name="host",
                cli_name="Host",
                display_name="Host",
                type=ParameterType.String,
                description="Computer from which to remove the file.",
                parameter_group_info=[
                    ParameterGroupInfo(required=False),
                ],
            ),
        ]

    def build_file_path(self, parsed_info):
        if parsed_info['host']:
            file_path = "\\\\{}\\{}\\{}".format(
                parsed_info['host'],
                parsed_info['folder_path'],
                parsed_info['file_name'])
        else:
            file_path = os.path.join(
                parsed_info['folder_path'],
                parsed_info['file_name'])
        return file_path

    def parse_file_path(self, file_path):
        unc_match = re.match(r'^\\\\([^\\]+)\\(.+)$', file_path)
        if unc_match:
            host = unc_match.group(1)
            folder_path = unc_match.group(2)
            file_name = None
            if folder_path:
                file_name = os.path.basename(folder_path)
                folder_path = os.path.dirname(folder_path)
        else:
            normalized_path = os.path.normpath(file_path)
            folder_path, file_name = os.path.split(normalized_path)
            host = None
            if not file_name:
                file_name = None
            if '/' in file_path:
                folder_path = folder_path.replace('\\', '/')
        return {
            'host': host,
            'folder_path': folder_path,
            'file_name': file_name
        }

    async def parse_arguments(self):
        if (len(self.raw_command_line) > 0):
            if (self.raw_command_line[0] == "{"):
                temp_json = json.loads(self.raw_command_line)
                if "file" in temp_json:
                    self.add_arg("path", temp_json["path"])
                    self.add_arg("host", temp_json["host"])
                    self.add_arg("file", temp_json["file"])
                else:
                    self.add_arg("path", temp_json["path"])
                    self.add_arg("host", temp_json["host"])
            else:
                path_parts = self.parse_file_path(
                    self.raw_command_line)
                combined_path = self.build_file_path({
                    "host": "",
                    "folder_path": path_parts["folder_path"],
                    "file_name": path_parts["file_name"]})
                self.add_arg("path", combined_path)
                self.add_arg("host", path_parts["host"])


class RmCommand(CommandBase):
    cmd = "rm"
    needs_admin = False
    script_only = True
    depends_on = "file-utils"
    plugin_libraries = []
    help_cmd = "rm [path]"
    description = "Remove a file"
    version = 1
    supported_ui_features = ["file_browser:remove"]
    author = "@checkymander"
    argument_class = RmArguments
    attackmapping = ["T1070.004", "T1565"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        host = taskData.args.get_arg("host")
        params = {
            "action": "rm",
            "path": taskData.args.get_arg("path"),
        }
        if host:
            params["host"] = host

        file_arg = taskData.args.get_arg("file")
        if file_arg:
            params["file"] = file_arg

        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="file-utils",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps(params)
        )
        await SendMythicRPCTaskCreateSubtask(subtask)

        resp = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        resp.DisplayParams = "-Path {}".format(
            taskData.args.get_arg("path"))
        if host:
            resp.DisplayParams += " -Host {}".format(host)
        return resp

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
