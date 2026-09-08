from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *

class KillArguments(TaskArguments):

    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="id",
                cli_name="id",
                display_name="id",
                type=ParameterType.Number,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="ID",
                        ui_position=0,
                    ),
                ]
            ),
            CommandParameter(
                name="name",
                cli_name="name",
                display_name="name",
                type=ParameterType.String,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Name",
                        ui_position=0,
                    ),
                ]
            ),
            CommandParameter(
                name="tree",
                cli_name="tree",
                display_name="Tree",
                type=ParameterType.Boolean,
                description="Include child processes in the kill",
                default_value=False,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="ID",
                        ui_position=1,
                    ),                    
                    ParameterGroupInfo(
                        required=False,
                        group_name="Name",
                        ui_position=1,
                    )
                ],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) == 0:
            raise Exception("No PID given.")
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            try:
                process_id = int(self.command_line)
            except ValueError:
                self.add_arg("name", self.command_line, ParameterType.String)
            else:
                self.add_arg("id", process_id, ParameterType.Number)
        self._validate_target()

    def _validate_target(self):
        process_id = self.get_arg("id")
        process_name = self.get_arg("name")
        has_id = process_id is not None
        has_name = process_name is not None
        if has_id == has_name:
            raise ValueError("Kill requires exactly one of id or name.")
        if has_id and (
            isinstance(process_id, bool)
            or not isinstance(process_id, int)
            or process_id <= 0
        ):
            raise ValueError("Kill id must be a positive integer.")
        if has_name and (
            not isinstance(process_name, str) or not process_name.strip()
        ):
            raise ValueError("Kill name must be a nonempty string.")
        

class killCommand(CommandBase):
    cmd = "kill"
    needs_admin = False
    help_cmd = "kill [id] [-tree True/False]"
    description = "Kill a process specified by an ID"
    version = 1
    author = "@checkymander"
    argument_class = KillArguments
    attackmapping = ["T1106"]
    supported_ui_features = ["kill"]
    attributes = CommandAttributes(
    )


    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass