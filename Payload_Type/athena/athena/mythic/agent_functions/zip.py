from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *

class ZipArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.ChooseOne,
                choices=["compress", "download", "inspect"],
                description="Action to perform",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                    )
                ],
            ),
            CommandParameter(
                name="source",
                type=ParameterType.String,
                description="Source directory to zip (compress/download)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=False,
                    )
                ],
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Destination path for the zip file (compress/download)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=False,
                    )
                ],
            ),
            CommandParameter(
                name="path",
                type=ParameterType.String,
                description="Path to zip file to inspect",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=3,
                        required=False,
                    )
                ],
            ),
            CommandParameter(
                name="force",
                type=ParameterType.Boolean,
                description="Force in-memory storage of large zip files (download)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=4,
                        required=False,
                    )
                ],
            ),
        ]

    async def parse_arguments(self):
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            cmds = self.split_commandline()
            if len(cmds) >= 2:
                self.add_arg("action", "compress")
                self.add_arg("source", cmds[0])
                self.add_arg("destination", cmds[1])
            else:
                raise Exception(
                    "Invalid arguments. Use JSON or: zip <source> <destination>"
                )


class ZipCommand(CommandBase):
    cmd = "zip"
    needs_admin = False
    help_cmd = "zip -action compress -source /path -destination /out.zip"
    description = (
        "Zip operations: compress a directory, download a directory "
        "as a zip, or inspect a zip file's contents."
    )
    version = 2
    author = "@checkymander"
    argument_class = ZipArguments
    attackmapping = ["T1570", "T1560.001"]
    attributes = CommandAttributes()

    async def create_go_tasking(
        self, taskData: PTTaskMessageAllData
    ) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        action = taskData.args.get_arg("action")
        if action == "compress":
            response.DisplayParams = (
                f"-Action compress -Source {taskData.args.get_arg('source')} "
                f"-Destination {taskData.args.get_arg('destination')}"
            )
        elif action == "download":
            response.DisplayParams = (
                f"-Action download -Source {taskData.args.get_arg('source')}"
            )
            dest = taskData.args.get_arg("destination")
            if dest:
                response.DisplayParams += f" -Destination {dest}"
        elif action == "inspect":
            response.DisplayParams = (
                f"-Action inspect -Path {taskData.args.get_arg('path')}"
            )
        return response

    async def process_response(
        self, task: PTTaskMessageAllData, response: any
    ) -> PTTaskProcessResponseMessageResponse:
        pass
