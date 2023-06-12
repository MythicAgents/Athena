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


    #Path parsing originally by @djhohnstein https://github.com/MythicAgents/Apollo/blob/master/Payload_Type/apollo/mythic/agent_functions/ls.py
    async def strip_host_from_path(self, path):
        host = ""
        if path[0] == "\\" and path[1] == "\\": #If the path starts with a UNC path, strip it out
            final = path.find("\\", 2) #Find the next slash after the first two e.g. \\host\path it would find the third slash
            if final != -1:
                host = path[2:final] #Set the host to the string between the first two slashes and the third slash
                path = path[final+1:] #Set the path to the string after the third slash
        return (host, path) #Return the host and path

    #Argument parsing originally by @djhohnstein https://github.com/MythicAgents/Apollo/blob/master/Payload_Type/apollo/mythic/agent_functions/ls.py
    #Potential inputs:
        
    #FileBrowser File
    #{"host":"DESKTOP-GRJNOH2","path":"C:\\Users\\scott\\Downloads","full_path":"C:\\Users\\scott\\Downloads\\donut.tar.gz","file":"donut.tar.gz"}
    
    #FileBrowser Folder
    #{"host":"DESKTOP-GRJNOH2","path":"C:\\Users\\scott\\Downloads","full_path":"C:\\Users\\scott\\Downloads\\donut","file":"donut"}
    
    #cmdline: C:\
    #{"path": "C:", "host": ""}

    #cmdline: C:\Users
    #{"path": "C:\\Users", "host": ""}

    #cmdline: localhost C:\users\scott
    #{"host": "localhost", "path": "C:\\users\\scott"}
    async def parse_arguments(self):
        print(self.command_line)
        if len(self.command_line) == 0:
            self.add_arg("host", "")
            self.add_arg("path", ".")
        else:
            if self.command_line[0] == '{': #This is a file browser or modal input
                #Potential inputs:
                #FileBrowser File
                #{"host":"DESKTOP-GRJNOH2","path":"C:\\Users\\scott\\Downloads","full_path":"C:\\Users\\scott\\Downloads\\donut.tar.gz","file":"donut.tar.gz"}
                #FileBrowser Folder
                #{"host":"DESKTOP-GRJNOH2","path":"C:\\Users\\scott\\Downloads","full_path":"C:\\Users\\scott\\Downloads\\donut","file":"donut"}
                #TODO: See how UNC paths are handled in Mythic and if they are handled properly, remove the strip_host_from_path function
                temp_json = json.loads(self.command_line)


                if("full_path" in temp_json):
                    self.add_arg("path", temp_json["full_path"])
                else:
                    if(temp_json["path"] is None):
                        self.add_arg("path", ".")
                    else:
                        self.add_arg("path", temp_json["path"])

                if("host" in temp_json):
                    self.add_arg("host", temp_json["host"])
            else: #This is regular command line
                #Host isn't required and should be properly parsed by Mythic
                #Just in case, if the path is nothing, set it to the current directory
                if self.get_arg("path") == "":
                    self.add_arg("path", ".")


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