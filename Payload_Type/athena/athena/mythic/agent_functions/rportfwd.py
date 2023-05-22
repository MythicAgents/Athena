from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class RPortFwdArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.ChooseOne,
                choices=["start", "stop"],
                default_value="start",
                description="Start or Stop rportfwd through this callback.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        name="start",
                    ),
                    ParameterGroupInfo(
                        name="stop",
                    )
                ]
            ),
            CommandParameter(
                name="lport",
                type=ParameterType.Number,
                description="Local port to open on host where agent is running",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=True,
                        name="start",
                    ),
                    ParameterGroupInfo(
                        ui_position=2,
                        required=True,
                        name="stop",
                    )
                ]
            ),
            CommandParameter(
                name="rport",
                type=ParameterType.Number,
                description="Remote port to connect to when a new connection comes in",
                default_value = 7000,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        name="start",
                    ),
                ]
            ),
            CommandParameter(
                name="rhost",
                type=ParameterType.Number,
                description="Remote IP to connect to when a new connection comes in",
                default_value = 7000,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        name="start",
                    ),
                ]
            ),
        ]

    async def parse_arguments(self):
        self.load_args_from_json_string(self.command_line)


class RPortFwdCommand(CommandBase):
    cmd = "rportfwd"
    needs_admin = False
    help_cmd = "rportfwd"
    description = "start or stop rportfwd."
    version = 1
    author = "@checkymander"
    argument_class = RPortFwdArguments
    #attackmapping = ["T1572"]
    attackmapping = []
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:

        resp = await SendMythicRPCProxyStartCommand(MythicRPCProxyStartMessage(
            TaskID=taskData.Task.ID,
            PortType="rpfwd",
            LocalPort=taskData.args.get_arg("lport"),
            RemoteIP = taskData.args.get_arg("rhost"),
            RemotePort = taskData.args.get_arg("rport"),
        ))

        if not resp.Success:
            raise Exception("Failed to start rportfwd: {}".format(resp.Error))
        else:
            taskData.args.remove_arg("rport")
            taskData.args.remove_arg("rhost")
            taskData.Task.DisplayParams = "Tasked Athena to forward port {} to {}:{}".format(taskData.args.get_arg("lport"), taskData.args.get_arg("rhost"), taskData.args.get_arg("rport"))
        return taskData

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp