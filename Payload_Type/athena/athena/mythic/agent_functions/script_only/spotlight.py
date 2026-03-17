from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class SpotlightArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="query", cli_name="query",
                display_name="Spotlight Query",
                type=ParameterType.String,
                description="Spotlight metadata query string",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default")
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("query", self.command_line)

class SpotlightCommand(CommandBase):
    cmd = "spotlight"
    needs_admin = False
    script_only = True
    depends_on = "jxa"
    plugin_libraries = []
    help_cmd = "spotlight -query \"kMDItemDisplayName == 'password*'\""
    description = "Search Spotlight metadata via JXA (macOS only)"
    version = 1
    author = "@checkymander"
    argument_class = SpotlightArguments
    attackmapping = ["T1083"]
    attributes = CommandAttributes(
        supported_os=[SupportedOS.MacOS],
    )
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        query = taskData.args.get_arg("query")
        jxa_code = (
            f"var query = $.NSMetadataQuery.alloc.init;"
            f"query.predicate = $.NSPredicate.predicateWithFormat('{query}');"
            f"query.startQuery();"
            f"$.NSRunLoop.currentRunLoop"
            f".runUntilDate($.NSDate.dateWithTimeIntervalSinceNow(5));"
            f"query.stopQuery();"
            f"var results = [];"
            f"for (var i = 0; i < query.resultCount; i++) {{"
            f"  results.push(query.resultAtIndex(i).valueForAttribute('kMDItemPath'));"
            f"}};"
            f"JSON.stringify(results);"
        )
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="jxa",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({"code": jxa_code})
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
