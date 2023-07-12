from mythic_container.MythicCommandBase import *  # import the basics
from mythic_container.MythicRPC import *
import json
from .athena_utils import message_converter


class DownloadArguments(TaskArguments):

    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="file",
                cli_name="Path",
                display_name="Path to file to download.",
                type=ParameterType.String,
                description="File to download.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=2
                    )
                ]),
            CommandParameter(
                name="host",
                cli_name="Host",
                display_name="Host",
                type=ParameterType.String,
                description="File to download.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    ),
                ]),
        ]

    async def parse_arguments(self):
        if len(self.command_line) == 0:
            raise Exception("Require a path to download.\n\tUsage: {}".format(DownloadCommand.help_cmd))
        filename = ""
        if self.command_line[0] == '"' and self.command_line[-1] == '"': #Remove double quotes if they exist
            self.command_line = self.command_line[1:-1]
            filename = self.command_line
        elif self.command_line[0] == "'" and self.command_line[-1] == "'": #Remove single quotes if they exist
            self.command_line = self.command_line[1:-1]
            filename = self.command_line
        elif self.command_line[0] == "{": #This is from JSON
            args = json.loads(self.command_line)
            if args.get("path") is not None and args.get("file") is not None: #If we have a path and a file it's likely from file browser
                # Then this is a filebrowser thing
                if args["path"][-1] == "\\": #Path already has a trailing slash so just append the file
                    self.add_arg("file", args["path"] + args["file"])
                else: #Path is missing a trailing slash so add it and then append the file
                    self.add_arg("file", args["path"] + "\\" + args["file"])
                self.add_arg("host", args["host"]) #Set the host
            else:
                # got a modal popup or parsed-cli
                self.load_args_from_json_string(self.command_line)
                if self.get_arg("host"): #Check if a host was set
                    if ":" in self.get_arg("host"): #If the host was set, but the path contains a : then it's unneeded.
                        if self.get_arg("file"):
                            self.add_arg("file", self.get_arg("host") + " " + self.get_arg("file"))
                        else:
                            self.add_arg("file", self.get_arg("host"))
                        self.remove_arg("host")
        else:
            filename = self.command_line

        if filename != "":
            if filename[:2] == "\\\\":
                # UNC path
                filename_parts = filename.split("\\")
                if len(filename_parts) < 4:
                    raise Exception("Illegal UNC path or no file could be parsed from: {}".format(filename))
                self.add_arg("host", filename_parts[2])
                self.add_arg("file", "\\".join(filename_parts[3:]))
            else:
                self.add_arg("file", filename)
                self.remove_arg("host")


class DownloadCommand(CommandBase):
    cmd = "download"
    needs_admin = False
    help_cmd = "download [path/to/file]"
    description = "Download a file off the target system."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    supported_ui_features = ["file_browser:download"]
    is_upload_file = False
    is_remove_file = False
    is_download_file = True
    author = "@checkymander"
    argument_class = DownloadArguments
    attackmapping = ["T1020", "T1030", "T1041"]
    browser_script = BrowserScript(script_name="download", author="@its_a_feature_")
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        if taskData.args.get_arg("host"):
            response.DisplayParams = "-Host {} -Path {}".format(taskData.args.get_arg("host"), taskData.args.get_arg("file"))
        else:
            response.DisplayParams = "-Path {}".format(taskData.args.get_arg("file"))
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp