from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class SocksArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            # CommandParameter(
            #     name="action",
            #     type=ParameterType.ChooseOne,
            #     choices=["start", "stop"],
            #     default_value="start",
            #     description="Start or Stop socks through this callback.",
            #     parameter_group_info=[
            #         ParameterGroupInfo(
            #             ui_position=1
            #         )
            #     ]
            # ),
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

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        resp = await SendMythicRPCProxyStartCommand(MythicRPCProxyStartMessage(
            TaskID=taskData.Task.ID,
            PortType="socks",
            Port=taskData.args.get_arg("port")
        ))

        if not resp.Success:
            response.TaskStatus = MythicStatus.Error
            response.Stderr = resp.Error
            await SendMythicRPCResponseCreate(MythicRPCResponseCreateMessage(
                TaskID=taskData.Task.ID,
                Response=resp.Error.encode()
            ))
        else:
            response.DisplayParams = "Started SOCKS5 server on port {}".format(taskData.args.get_arg("port"))
            response.TaskStatus = MythicStatus.Success
            response.Completed = True
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp