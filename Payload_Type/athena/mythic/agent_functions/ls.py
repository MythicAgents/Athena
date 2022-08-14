from mythic_payloadtype_container.MythicCommandBase import *
import json


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
                        ui_position=0
                    ),
                ])
        ]


    #Path parsing originally by @djhohnstein https://github.com/MythicAgents/Apollo/blob/master/Payload_Type/apollo/mythic/agent_functions/ls.py
    async def strip_host_from_path(self, path):
        host = ""
        if path[0] == "\\" and path[1] == "\\":
            final = path.find("\\", 2)
            if final != -1:
                host = path[2:final]
                path = path[final+1:]
        return (host, path)

    #Argument parsing originally by @djhohnstein https://github.com/MythicAgents/Apollo/blob/master/Payload_Type/apollo/mythic/agent_functions/ls.py
    async def parse_arguments(self):
        if len(self.command_line) > 0: #Make sure our command line has stuff
            if self.command_line[0] == '{':
                temp_json = json.loads(self.command_line)
                if "file" in temp_json.keys(): #This is an unsupported flow
                    # we came from the file browser
                    host = ""
                    path = temp_json['path']
                    if 'file' in temp_json and temp_json['file'] != "":
                        path += "\\" + temp_json['file']
                    if 'host' in temp_json:
                        host = temp_json['host']

                    self.add_arg("host", host)
                    self.add_arg("path", path)
                    self.add_arg("file_browser", "true")
                else:
                    self.load_args_from_json_string(self.command_line) #This would pass a Host and path (not including file)
            else:
                args = await self.strip_host_from_path(self.command_line) #This would pass a Host and Path based on cmdline places
                self.add_arg("host", args[0])
                self.add_arg("path", args[1])
                self.add_arg("file_browser", "false")
        else: #Can this ever flag?
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
    browser_script = [BrowserScript(script_name="ls", author="@tr41nwr3ck", for_new_ui=True)]
    attributes = CommandAttributes(
        load_only=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        host = task.args.get_arg("host")
        path = task.args.get_arg("path")
        if host:
            task.display_params = "{} on {}".format(path, host)
        else:
            task.display_params = path
        return task

    async def process_response(self, response: AgentResponse):
        pass
