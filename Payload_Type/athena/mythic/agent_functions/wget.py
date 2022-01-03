from mythic_payloadtype_container.MythicCommandBase import *
import json


class WgetArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = {
            "url": CommandParameter(name="Url", type=ParameterType.String, description="The URL to download."),
            "cookies": CommandParameter(name="Cookies", type=ParameterType.String, description="JSON representation of cookies to pass with the request"),
            "headers": CommandParameter(name="Headers", type=ParameterType.String, description="JSON representation of host headers to pass in the request"),
            "method": CommandParameter(name="Method", type=ParameterType.String, description="HTTP Method to use (Default=GET)", default_value="get"),
            "body": CommandParameter(name="Body", type=ParameterType.String, description="The body content of the request to send.")
        }

    async def parse_arguments(self):
        if len(self.command_line) == 0:
            raise Exception("Require a url to download.\n\tUsage: {}".format(WgetCommand.help_cmd))
        url = ""
        # Remove DoubleQuotes from cmdline
        if self.command_line[0] == '"' and self.command_line[-1] == '"':
            self.command_line = self.command_line[1:-1]
            url = self.command_line
        # #Remove SingleQuotes from cmdline
        elif self.command_line[0] == "'" and self.command_line[-1] == "'":
            self.command_line = self.command_line[1:-1]
            url = self.command_line
        elif self.command_line[0] == "{":
            # got a modal popup
            self.load_args_from_json_string(self.command_line)
        else:
            url = self.command_line

        if url != "":
            self.args["url"].value = url


class WgetCommand(CommandBase):
    cmd = "wget"
    needs_admin = False
    help_cmd = "wget https://example.com/index.html"
    description = "Download a file off the target system."
    version = 2
    is_exit = False
    is_file_browse = False
    is_process_list = False
    supported_ui_features = ["file_browser:download"]
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = WgetArguments
    attackmapping = ["T1020", "T1030", "T1041"]
    # browser_script = BrowserScript(script_name="download", author="@its_a_feature_")

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, response: AgentResponse):
        pass