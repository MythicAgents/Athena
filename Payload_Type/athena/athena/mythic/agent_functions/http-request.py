from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class HttpRequestArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="url", cli_name="url",
                display_name="URL",
                type=ParameterType.String,
                description="Target URL",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="method", cli_name="method",
                display_name="Method",
                type=ParameterType.ChooseOne,
                choices=["GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"],
                default_value="GET",
                description="HTTP method",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="body", cli_name="body",
                display_name="Body",
                type=ParameterType.String,
                description="Request body",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
            CommandParameter(
                name="headers", cli_name="headers",
                display_name="Headers (JSON)",
                type=ParameterType.String,
                description='Custom headers as JSON object, e.g. {"Authorization": "Bearer token"}',
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=3)
                ]
            ),
            CommandParameter(
                name="cookies", cli_name="cookies",
                display_name="Cookies (JSON)",
                type=ParameterType.String,
                description='Cookies as JSON object, e.g. {"session": "abc123"}',
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=4)
                ]
            ),
            CommandParameter(
                name="timeout", cli_name="timeout",
                display_name="Timeout (seconds)",
                type=ParameterType.Number,
                description="Request timeout in seconds",
                default_value=30,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=5)
                ]
            ),
            CommandParameter(
                name="follow_redirects", cli_name="follow_redirects",
                display_name="Follow Redirects",
                type=ParameterType.Boolean,
                description="Follow HTTP redirects",
                default_value=True,
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=6)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("url", self.command_line.strip())

class HttpRequestCommand(CommandBase):
    cmd = "http-request"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "http-request -url https://example.com -method GET"
    description = "Make HTTP requests (GET/POST/PUT/DELETE/PATCH/HEAD/OPTIONS)"
    version = 1
    author = "@checkymander"
    argument_class = HttpRequestArguments
    attackmapping = ["T1071.001"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        response.DisplayParams = f"{taskData.args.get_arg('method')} {taskData.args.get_arg('url')}"
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
