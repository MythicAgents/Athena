from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class MvArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="source",
                type=ParameterType.String,
                description="Source file to move.",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Source will move to this location",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
        ]

    def split_commandline(self):
        if self.command_line[0] == "{":
            raise Exception(
                "split_commandline expected string, but got JSON object: "
                + self.command_line)
        inQuotes = False
        curCommand = ""
        cmds = []
        for x in range(len(self.command_line)):
            c = self.command_line[x]
            if c == '"' or c == "'":
                inQuotes = not inQuotes
            if (not inQuotes and c == ' '):
                cmds.append(curCommand)
                curCommand = ""
            else:
                curCommand += c
        if curCommand != "":
            cmds.append(curCommand)
        for x in range(len(cmds)):
            if cmds[x][0] == '"' and cmds[x][-1] == '"':
                cmds[x] = cmds[x][1:-1]
            elif cmds[x][0] == "'" and cmds[x][-1] == "'":
                cmds[x] = cmds[x][1:-1]
        return cmds

    async def parse_arguments(self):
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            cmds = self.split_commandline()
            if len(cmds) != 2:
                raise Exception(
                    "Expected two arguments to mv, but got: "
                    "{}\n\tUsage: {}".format(cmds, MvCommand.help_cmd))
            self.add_arg("source", cmds[0])
            self.add_arg("destination", cmds[1])


class MvCommand(CommandBase):
    cmd = "mv"
    needs_admin = False
    script_only = True
    depends_on = "file-utils"
    plugin_libraries = []
    help_cmd = "mv"
    description = "Move a file from one location to another."
    version = 1
    author = "@checkymander"
    argument_class = MvArguments
    attackmapping = ["T1106"]
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
                "action": "mv",
                "source": taskData.args.get_arg("source"),
                "destination": taskData.args.get_arg("destination"),
            })
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        resp = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        resp.DisplayParams = "-Path {} -Destination {}".format(
            taskData.args.get_arg("source"),
            taskData.args.get_arg("destination"))
        return resp

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
