from ..athena_utils.plugin_utilities import default_completion_callback
from ..athena_utils.mythicrpc_utilities import (
    get_mythic_file,
    get_mythic_file_name,
)
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class NslookupArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="hosts",
                type=ParameterType.String,
                description="Comma separated list of hosts",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                    )
                ]
            ),
            CommandParameter(
                name="inputlist",
                type=ParameterType.File,
                description=(
                    "List of hosts in a newline separated file"
                ),
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="TargetList",
                    )
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hosts", self.command_line)
        else:
            raise ValueError("Missing arguments")


class NsLookupCommand(CommandBase):
    cmd = "nslookup"
    needs_admin = False
    script_only = True
    depends_on = "dns"
    plugin_libraries = []
    help_cmd = "nslookup DC1.gaia.local,FS1.gaia.local"
    description = "Perform bulk DNS lookups on provided hosts"
    version = 2
    author = "@checkymander"
    argument_class = NslookupArguments
    attackmapping = ["T1106"]
    attributes = CommandAttributes()
    completion_functions = {
        "command_callback": default_completion_callback,
    }

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )

        group_name = taskData.args.get_parameter_group_name()
        params = {"action": "bulk"}

        if group_name == "TargetList":
            file_id = taskData.args.get_arg("inputlist")
            encoded_contents = await get_mythic_file(file_id)
            original_name = await get_mythic_file_name(file_id)
            params["targetlist"] = encoded_contents
            response.DisplayParams = original_name
        else:
            params["hosts"] = taskData.args.get_arg("hosts")
            response.DisplayParams = taskData.args.get_arg("hosts")

        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="dns",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps(params),
            ParameterGroupName="Bulk",
        )
        await SendMythicRPCTaskCreateSubtask(subtask)

        return response

    async def process_response(
        self,
        task: PTTaskMessageAllData,
        response: any,
    ) -> PTTaskProcessResponseMessageResponse:
        pass
