from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from .athena_utils import message_converter
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
                    "Invalid number of arguments given. Expected two, but received: {}\n\tUsage: {}".format(
                        cmds, CpCommand.help_cmd
                    )
                )
            self.add_arg("source", cmds[0])
            self.add_arg("destination", cmds[1])


class CpCommand(CommandBase):
    cmd = "cp"
    needs_admin = False
    help_cmd = "cp <source> <destination>"
    description = "Copy a file from one location to another."
    version = 1
    author = "@checkymander"
    argument_class = CpArguments
    attackmapping = ["T1570"]
    attributes = CommandAttributes(
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        response.DisplayParams = "-Source {} -Destination {}".format(
            taskData.args.get_arg("source"), taskData.args.get_arg("destination")
        )
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp