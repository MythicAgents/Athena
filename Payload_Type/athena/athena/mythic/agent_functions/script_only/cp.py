from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class CpArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="source",
                type=ParameterType.String,
                description="Source file to copy.",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Source will copy to this location",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
        ]

    async def parse_arguments(self):
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            cmds = self.split_commandline()
            if len(cmds) != 2:
                raise Exception(
                    "Invalid number of arguments given. Expected two, "
                    "but received: {}\n\tUsage: {}".format(
                        cmds, CpCommand.help_cmd))
            self.add_arg("source", cmds[0])
            self.add_arg("destination", cmds[1])


class CpCommand(CommandBase):
    cmd = "cp"
    needs_admin = False
    script_only = True
    depends_on = "file-utils"
    plugin_libraries = []
    help_cmd = "cp <source> <destination>"
    description = "Copy a file from one location to another."
    version = 1
    author = "@checkymander"
    argument_class = CpArguments
    attackmapping = ["T1570"]
    attributes = CommandAttributes()
    completion_functions = {"command_callback": default_completion_callback}

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="file-utils",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps({
                "action": "cp",
                "source": taskData.args.get_arg("source"),
                "destination": taskData.args.get_arg("destination"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        resp = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        resp.DisplayParams = "-Source {} -Destination {}".format(
            taskData.args.get_arg("source"),
            taskData.args.get_arg("destination"))
        return resp

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
