from mythic_payloadtype_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
import os
# import the code for interacting with Files on the Mythic server
from mythic_payloadtype_container.MythicRPC import *
from os import listdir
from os.path import isfile, join

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class DsqueryArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="username",
                type=ParameterType.String,
                description="Username to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="password",
                type=ParameterType.String,
                description="Password to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="domain",
                type=ParameterType.String,
                description="Password to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),            
            CommandParameter(
                name="ldapfilter",
                type=ParameterType.String,
                description="Password to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="objectcategory",
                type=ParameterType.ChooseOne,
                choices=[
                    "user",
                    "group",
                    "ou",
                    "computer",
                    "*",
                ],
                description="Password to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="searchbase",
                type=ParameterType.String,
                description="Password to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="server",
                type=ParameterType.String,
                description="Password to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
            CommandParameter(
                name="properties",
                type=ParameterType.String,
                description="Password to bind with",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=1
                    )
                ],
            ),
        ]

    async def get_libraries(self, callback: dict) -> [str]:
        # Get a directory listing based on the current OS Version
        file_names = []
        if callback["payload"]["os"] == "Windows":
            mypath = os.path.join("/","Mythic","agent_code", "AthenaPlugins", "bin", "windows")
        elif callback["payload"]["os"] == "Linux":
            mypath = os.path.join("/","Mythic","agent_code", "AthenaPlugins", "bin", "linux")
        elif callback["payload"]["os"] == "macOS":
            mypath = os.path.join("/","Mythic","agent_code", "AthenaPlugins", "bin", "macos")
        else:
            file_names.append("No Supported Libraries")
            return file_names

        file_names = [f for f in listdir(mypath) if isfile(join(mypath, f))]
        return file_names

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class DsqueryCommand(CommandBase):
    cmd = "dsquery"
    needs_admin = False
    help_cmd = "dsquery"
    description = "Run an LDAP Query against a Domain Controller"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = False
    is_upload_file = False
    author = "@checkymander"
    argument_class = DsqueryArguments
    attackmapping = []
    browser_script = None
    attributes = CommandAttributes(
        load_only=True,
    )

    # this function is called after all of your arguments have been parsed and validated that each "required" parameter has a non-None value
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        groupName = task.args.get_parameter_group_name()
        if groupName == "InternalLib":
            # Using an included library
            if task.callback.payload["os"] == "Windows":
                dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", "windows",
                                       f"{task.args.get_arg('libraryname')}")
            elif task.callback.payload["os"] == "Linux":
                dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", "linux",
                                       f"{task.args.get_arg('libraryname')}")
            elif task.callback.payload["os"] == "macOS":
                dllFile = os.path.join(self.agent_code_path, "AthenaPlugins", "bin", "macos",
                                       f"{task.args.get_arg('libraryname')}")
            dllBytes = open(dllFile, 'rb').read()
            encodedBytes = base64.b64encode(dllBytes)
            task.args.add_arg("assembly", encodedBytes.decode(),
                              parameter_group_info=[ParameterGroupInfo(group_name="InternalLib")])
            task.display_params = f"{task.args.get_arg('libraryname')}"
        elif groupName == "Default":
            # Get contents of the file
            file_resp = await MythicRPC().execute("get_file",
                                                  file_id=task.args.get_arg("library"),
                                                  task_id=task.id,
                                                  get_contents=True)
            if file_resp.status == MythicRPCStatus.Success:
                if len(file_resp.response) > 0:
                    task.args.add_arg("assembly", file_resp.response[0]["contents"],
                                      parameter_group_info=[ParameterGroupInfo(group_name="Default")])
                    task.display_params = f"{file_resp.response[0]['filename']}"
                else:
                    raise Exception("Failed to find that file")
            else:
                raise Exception("Error from Mythic trying to get file: " + str(file_resp.error))
        return task

    async def process_response(self, response: AgentResponse):
        pass


