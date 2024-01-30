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
                description="Start or Stop rportfwd in this callback.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        name="Default",
                    ),
                ]
            ),
            CommandParameter(
                name="lport",
                type=ParameterType.Number,
                description="Local port to open on host where agent is running",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        name="Default",
                    ),
                ]
            ),
            CommandParameter(
                name="rport",
                type=ParameterType.Number,
                description="Remote port to connect to when a new connection comes in",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=False,
                        name="Default",
                    ),
                ]
            ),
            CommandParameter(
                name="rhost",
                type=ParameterType.String,
                description="Remote IP to connect to when a new connection comes in",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=3,
                        required=False,
                        name="Default",
                    ),
                ]
            ),
        ]

    async def parse_arguments(self):
        self.load_args_from_json_string(self.command_line)


class RPortFwdCommand(CommandBase):
    cmd = "rportfwd"
    needs_admin = False
    help_cmd = "rportfwd start -lport=1234 -rhost=127.0.0.1 -rport=1234"
    description = "start or stop rportfwd."
    version = 1
    author = "@checkymander"
    argument_class = RPortFwdArguments
    attackmapping = ["T1090"]
    attributes = CommandAttributes(
        load_only=False,
        builtin=False
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        if taskData.args.get_arg("action") == "stop":
            resp = await SendMythicRPCProxyStartCommand(MythicRPCProxyStopMessage(
                TaskID = taskData.Task.ID,
                PortType = "rpfwd",
                Port = taskData.args.get_arg("lport")
            ))

            response = PTTaskCreateTaskingMessageResponse(
                    TaskID = taskData.Task.ID,
                    Success = True,
                    DisplayParams = "Tasked Athena to stop listening on port {}".format(taskData.args.get_arg("lport"))
                )
            return response       
        else:
            if not taskData.args.get_arg("rhost"):
                raise Exception("Missing remote host to forward to.")
            if taskData.args.get_arg("rport") == 0:
                raise Exception("Missing remote port to forward to.")

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
                response = PTTaskCreateTaskingMessageResponse(
                    TaskID = taskData.Task.ID,
                    Success = True,
                    DisplayParams = "Tasked Athena to forward port {} to {}:{}".format(taskData.args.get_arg("lport"), taskData.args.get_arg("rhost"), taskData.args.get_arg("rport")),
                )
                taskData.args.remove_arg("rport")
                taskData.args.remove_arg("rhost")
                return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp