from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *

from Payload_Type.athena.athena.mythic.agent_functions.athena_messages import message_converter


class UnlinkArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="id",
                display_name="Agent ID",
                type=ParameterType.String,
                description="ID of the agent to unlink",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0
                    )
                ]
            ),]

    async def parse_arguments(self):
        pass


class UnlinkCommand(CommandBase):
    cmd = "unlink"
    needs_admin = False
    help_cmd = "unlink"
    description = "Unlink from the existing forwarder"
    version = 1
    author = "@checkymander"
    attackmapping = []
    argument_class = UnlinkArguments
    attributes = CommandAttributes(
        builtin=True
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        user_output = response["message"]
        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))
        return resp
