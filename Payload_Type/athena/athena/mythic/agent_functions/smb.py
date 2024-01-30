from mythic_container.MythicCommandBase import *
import json
from mythic_container.MythicRPC import *
from .athena_utils import message_converter


class SmbArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=[
                    "link",
                    "unlink",
                    "list",
                ],
                description="Action to perform",
                default_value="list",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
            CommandParameter(
                name="hostname",
                type=ParameterType.String,
                description="The host to connect to.",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            CommandParameter(
                name="pipename",
                type=ParameterType.String,
                description="The name of the pipe the agent is listening on.",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("action", self.command_line.split()[0])
                self.add_arg("hostname", self.command_line.split()[1])
                self.add_arg("pipename", self.command_line.split()[2])


class SmbCommand(CommandBase):
    cmd = "smb"
    needs_admin = False
    help_cmd = """smb link <hostname> <pipename>
smb unlink
smb list
    """
    description = "Initiate a connection with a SMB Athena agent."
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_remove_file = True
    is_upload_file = False
    author = "@checkymander"
    argument_class = SmbArguments
    attackmapping = []
    attributes = CommandAttributes(
        load_only=False,
        builtin=False,
        supported_os=[SupportedOS.Windows]
    )
    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )


        if(taskData.args.get_arg("action") == "link"):
            response.DisplayParams = "to {} with pipe {}".format(taskData.args.get_arg("hostname"), taskData.args.get_arg("pipename"))
        elif(taskData.args.get_arg("action") == "unlink"):
            response.DisplayParams = "from {} with pipe {}".format(taskData.args.get_arg("hostname"), taskData.args.get_arg("pipename"))
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp