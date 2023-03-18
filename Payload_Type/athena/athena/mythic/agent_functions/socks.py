from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *


class SocksArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.ChooseOne,
                choices=["start", "stop"],
                default_value="start",
                description="Start or Stop socks through this callback.",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1
                    )
                ]
            ),
            CommandParameter(
                name="port",
                type=ParameterType.Number,
                description="Port number on Mythic server to open for socksv5",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2
                    )
                ]
            ),
        ]

    async def parse_arguments(self):
        self.load_args_from_json_string(self.command_line)


class SocksCommand(CommandBase):
    cmd = "socks"
    needs_admin = False
    help_cmd = "socks"
    description = "start or stop socks."
    version = 1
    author = "@checkymander"
    argument_class = SocksArguments
    #attackmapping = ["T1572"]
    attackmapping = []
    attributes = CommandAttributes(
        load_only=False,
        builtin=True
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        if task.args.get_arg("action") == "start":
            start_res = await SendMythicRPCProxyStartCommand(port=task.args.get_arg("port"), task_id=task.id)
            if not start_res.success:
                raise Exception(start_res.error)
        else:
            stop_res = await SendMythicRPCProxyStopCommand(port=task.args.get_arg("port"), task_id=task.id)
            if not stop_res:
                raise Exception(stop_res.error)
        return task

    async def process_response(self, response: AgentResponse):
        pass