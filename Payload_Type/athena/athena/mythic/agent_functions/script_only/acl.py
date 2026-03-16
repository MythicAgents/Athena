from ..athena_utils.plugin_utilities import default_ldap_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json


class AclArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="ldapfilter", cli_name="ldapfilter",
                display_name="LDAP Filter",
                type=ParameterType.String, default_value="",
                description="LDAP filter for target objects",
                parameter_group_info=[ParameterGroupInfo(required=False, group_name="Default", ui_position=0)]
            ),
            CommandParameter(
                name="searchbase", cli_name="searchbase",
                display_name="Search Base",
                type=ParameterType.String, default_value="",
                description="LDAP search base",
                parameter_group_info=[ParameterGroupInfo(required=False, group_name="Default", ui_position=1)]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


class AclCommand(CommandBase):
    cmd = "acl"
    needs_admin = False
    script_only = True
    depends_on = "ds"
    plugin_libraries = []
    help_cmd = "acl -ldapfilter \"(cn=Domain Admins)\""
    description = "Query ACLs on AD objects via LDAP"
    version = 1
    author = "@checkymander"
    argument_class = AclArguments
    attackmapping = ["T1087.002"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_ldap_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID, CommandName="ds",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "acl",
                "ldapfilter": taskData.args.get_arg("ldapfilter") or "",
                "searchbase": taskData.args.get_arg("searchbase") or "",
                "objectcategory": "*",
            }))
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task, response):
        pass
