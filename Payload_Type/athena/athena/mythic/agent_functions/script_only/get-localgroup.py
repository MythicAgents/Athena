from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json


class GetLocalGroupArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="hostname", type=ParameterType.String, default_value="",
                description="Server to scan",
                parameter_group_info=[ParameterGroupInfo(
                    required=False, group_name="Default", ui_position=0
                )]
            ),
            CommandParameter(
                name="group", type=ParameterType.String, default_value="",
                description="Group to enumerate",
                parameter_group_info=[ParameterGroupInfo(
                    required=False, group_name="Default", ui_position=1
                )]
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                parts = self.command_line.split()
                if len(parts) >= 1:
                    self.add_arg("hostname", parts[0])
                if len(parts) >= 2:
                    self.add_arg("group", parts[1])


class GetLocalGroupCommand(CommandBase):
    cmd = "get-localgroup"
    needs_admin = False
    script_only = True
    depends_on = "enum-windows"
    plugin_libraries = []
    help_cmd = "get-localgroup [-server <servername>] [-group <groupname>]"
    description = "Get localgroups on a host, or members of a group if a group is specified."
    version = 1
    author = "@checkymander"
    argument_class = GetLocalGroupArguments
    attackmapping = ["T1069", "T1069.001"]
    attributes = CommandAttributes(supported_os=[SupportedOS.Windows])
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="enum-windows",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "get-localgroup",
                "hostname": taskData.args.get_arg("hostname") or "",
                "group": taskData.args.get_arg("group") or "",
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
