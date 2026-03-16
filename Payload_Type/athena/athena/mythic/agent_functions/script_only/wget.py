from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class WgetArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="url", cli_name="url",
                display_name="URL",
                type=ParameterType.String,
                description="URL to fetch",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="method", cli_name="method",
                display_name="Method",
                type=ParameterType.ChooseOne,
                choices=["GET", "POST"],
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
                description="Request body (POST only)",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
            CommandParameter(
                name="headers", cli_name="headers",
                display_name="Headers (JSON)",
                type=ParameterType.String,
                description='Custom headers as JSON object',
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=3)
                ]
            ),
            CommandParameter(
                name="cookies", cli_name="cookies",
                display_name="Cookies (JSON)",
                type=ParameterType.String,
                description='Cookies as JSON object',
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=4)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("url", self.command_line.strip())

class WgetCommand(CommandBase):
    cmd = "wget"
    needs_admin = False
    script_only = True
    depends_on = "http-request"
    plugin_libraries = []
    help_cmd = "wget -url https://example.com"
    description = "Fetch a URL (simplified HTTP GET/POST)"
    version = 1
    author = "@checkymander"
    argument_class = WgetArguments
    attackmapping = ["T1071.001"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="http-request",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "url": taskData.args.get_arg("url"),
                "method": taskData.args.get_arg("method"),
                "body": taskData.args.get_arg("body"),
                "headers": taskData.args.get_arg("headers"),
                "cookies": taskData.args.get_arg("cookies"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
