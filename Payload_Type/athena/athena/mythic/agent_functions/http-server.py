from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

from .athena_utils import message_converter


class HttpServerArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.ChooseOne,
                description="Action to perform",
                choices=[
                   "start",
                   "list",
                   "remove"
                ],
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default" # Many Args
                    ),
                ],
            ),
            CommandParameter(
                name="port",
                type=ParameterType.Number,
                description="Local port to open on host where agent is running",
                default_value = 8080,
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=False,
                        group_name="Default",
                    )
                ]
            ),
            CommandParameter(
                name="fileName",
                type=ParameterType.String,
                description="Name of the file when hosting",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        group_name="Host a File",
                    )
                ]
            ),
            CommandParameter(
                name="file",
                type=ParameterType.File,
                description="File to host",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=True,
                        group_name="Host a File",
                    )
                ]
            ),
        ]

    async def parse_arguments(self):
        if(self.command_line.startswith('{')):
            self.load_args_from_json_string(self.command_line)


class HttpServerCommand(CommandBase):
    cmd = "http-server"
    needs_admin = False
    help_cmd = """http-server start 8080"""
    description = "Starts an in-memory HTTP Server"
    version = 1
    author = "@checkymander"
    argument_class = HttpServerArguments
    attackmapping = []
    attributes = CommandAttributes(
        load_only=False,
        builtin=False
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )

        if taskData.args.get_parameter_group_name() == "Host a File":
            fData = FileData()
            fData.AgentFileId = taskData.args.get_arg("file")
            file = await SendMythicRPCFileGetContent(fData)
            
            if file.Success:
                file_contents = base64.b64encode(file.Content)
                taskData.args.add_arg("fileContents", file_contents.decode("utf-8"), parameter_group_info=[ParameterGroupInfo(ui_position=2,
                                                                                                                              group_name= "Host a File",
                                                                                                                              required = True
                                                                                                                              )])
                taskData.args.add_arg("action", "host", parameter_group_info=[ParameterGroupInfo(ui_position=2,
                                                                                                group_name= "Host a File",
                                                                                                required = True
                                                                                                )])
                response.DisplayParams = "Hosting file {} at /{}".format(taskData.args.get_arg("fileName"), taskData.args.get_arg("fileName"))
            else:
                raise Exception("Failed to get file contents: " + file.Error)
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp