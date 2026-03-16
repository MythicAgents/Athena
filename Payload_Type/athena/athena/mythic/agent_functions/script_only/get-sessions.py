from ..athena_utils.plugin_utilities import default_completion_callback
from ..athena_utils.mythicrpc_utilities import get_mythic_file, get_mythic_file_name
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json


class GetSessionsArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="hosts", type=ParameterType.String, default_value="",
                description="Comma separated list of hosts",
                parameter_group_info=[ParameterGroupInfo(
                    required=True, group_name="Default"
                )]
            ),
            CommandParameter(
                name="inputlist", type=ParameterType.File,
                description="List of hosts in a newline separated file",
                parameter_group_info=[ParameterGroupInfo(
                    required=True, group_name="TargetList"
                )]
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hosts", self.command_line)


class GetSessionsCommand(CommandBase):
    cmd = "get-sessions"
    needs_admin = False
    script_only = True
    depends_on = "enum-windows"
    plugin_libraries = []
    help_cmd = "get-sessions DC1.gaia.local,FS1.gaia.local"
    description = "Perform NetSessionEnum on provided hosts (Windows only)"
    version = 1
    author = "@checkymander"
    argument_class = GetSessionsArguments
    attackmapping = []
    attributes = CommandAttributes(supported_os=[SupportedOS.Windows])
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        params = {"action": "get-sessions"}
        groupName = taskData.args.get_parameter_group_name()
        if groupName == "TargetList":
            encoded_file_contents = await get_mythic_file(taskData.args.get_arg("inputlist"))
            params["targetlist"] = encoded_file_contents
        else:
            params["hosts"] = taskData.args.get_arg("hosts")

        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="enum-windows",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps(params)
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
