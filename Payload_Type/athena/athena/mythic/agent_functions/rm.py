
from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from .athena_messages import message_converter


class RmArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="Path to file to remove",
                parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    ui_position=1,
                )],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                temp_json = json.loads(self.command_line)
                if "host" in temp_json:
                    self.add_arg("path", temp_json["path"] + "/" + temp_json["file"])
                else:
                    self.add_arg("path", temp_json["path"])
            else:
                self.add_arg("path", self.command_line)
        else:
            raise ValueError("Missing arguments")

class RmCommand(CommandBase):
    cmd = "rm"
    needs_admin = False
    help_cmd = "rm [path]"
    description = "Remove a file"
    version = 1
    supported_ui_features = ["file_browser:remove"]
    author = "@checkymander"
    #attackmapping = ["T1070.004", "T1565"]
    attackmapping = []
    argument_class = RmArguments
    attributes = CommandAttributes(
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        resp = MythicRPC().execute("create_artifact", task_id=task.id,
            artifact="fileManager.removeItemAtPathError",
            artifact_type="API Called",
        )
        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp