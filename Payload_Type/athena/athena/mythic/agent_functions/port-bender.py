from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class PortBenderArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="port",
                type=ParameterType.Number,
                description="Local port to open on host where agent is running",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        name="Default",
                    ),
                ]
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Remote IP to connect to when a new connection comes in",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        name="Default",
                    ),
                ]
            ),
        ]

    async def parse_arguments(self):
        if(self.command_line.startswith('{')):
            self.load_args_from_json_string(self.command_line)


class PortBenderCommand(CommandBase):
    cmd = "port-bender"
    needs_admin = False
    help_cmd = "port-bender 8080 192.168.12.13:8080"
    description = "Starts a port bender"
    version = 1
    author = "@checkymander"
    argument_class = PortBenderArguments
    attackmapping = ["T1090"]
    attributes = CommandAttributes(
        load_only=False,
        builtin=False
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        response.DisplayParams = "forwarding port {} to {}".format(taskData.args.get_arg("port"), taskData.args.get_arg("destination"))
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp