from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class DsqueryArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="username",
                type=ParameterType.String,
                description="Username for authentication",
                default_value="",
                required=True,
            ),
            CommandParameter(
                name="password",
                type=ParameterType.String,
                description="Password for authentication",
                default_value="",
                required=True,
            ),
            CommandParameter(
                name="domain",
                type=ParameterType.String,
                description="Target domain",
                default_value="",
                required=True,
            ),
            CommandParameter(
                name="objectcategory",
                type=ParameterType.String,
                description="Type of object to return (user,group,ou,computer,*)",
                default_value="",
                required=True,
            ),
            CommandParameter(
                name="properties",
                type=ParameterType.String,
                description="Comma-Separated list of LDAP Properties to return (* or all are also accepted)",
                default_value="",
            ),
            CommandParameter(
                name="ldapfilter",
                type=ParameterType.String,
                description="Specify an ldapfilter for your results",
                default_value="",
                
            ),
            CommandParameter(
                name="server",
                type=ParameterType.String,
                description="The server to target your ldap queries to, if one isn't provided Athena will attempt to automatically select a server",
                default_value="",
                
            ),
            CommandParameter(
                name="searchbase",
                type=ParameterType.String,
                description="The searchbase to restrict your query to.",
                default_value="",
                
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


class DsqueryCommand(CommandBase):
    cmd = "dsquery"
    needs_admin = False
    help_cmd = "dsquery"
    description = "Tasks Athena to get domain user information from Active Directory/LDAP."
    version = 1
    supported_ui_features = []
    author = "@checkymander"
    attackmapping = []
    argument_class = DsqueryArguments
    browser_script = [BrowserScript(script_name="dsquery", author="@tr41nwr3ck", for_new_ui=True)]


    async def create_tasking(self, task: MythicTask) -> MythicTask:
        resp = await MythicRPC().execute("create_artifact", task_id=task.id,
            artifact="$.NSApplication.sharedApplication.terminate",
            artifact_type="API Called",
        )
        return task

    async def process_response(self, response: AgentResponse):
        pass
