from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *
from .athena_utils.argument_utilities import (
    load_json_or_get_shorthand,
    require_nonempty_string,
    split_shorthand,
)

class ZipDlArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="source",
                type=ParameterType.String,
                description="Source file to copy.",
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
            CommandParameter(
                name="destination",
                type=ParameterType.String,
                description="Source will copy to this location",
                parameter_group_info=[ParameterGroupInfo(ui_position=1)],
            ),
            CommandParameter(
                name="write",
                type=ParameterType.Boolean,
                description="Write the zip to destination before downloading",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
            CommandParameter(
                name="force",
                type=ParameterType.Boolean,
                description="Force in memory storage of large zip files",
                parameter_group_info=[ParameterGroupInfo(ui_position=2)],
            ),
        ]

    async def parse_arguments(self):
        command_line = load_json_or_get_shorthand(
            self, "zip-dl", "zip-dl requires a source directory"
        )
        if command_line is None:
            self._validate_json_arguments()
        else:
            values = split_shorthand(command_line, "zip-dl")
            if len(values) not in (1, 2):
                raise ValueError("zip-dl requires a source and optional destination or -force option")

            self.add_arg("source", values[0])
            self.add_arg("write", False)
            self.add_arg("force", False)
            if len(values) == 2 and values[1].lower().startswith("-force="):
                force_value = values[1].split("=", 1)[1].lower()
                if force_value not in ("true", "false"):
                    raise ValueError("zip-dl force must be true or false")
                self.add_arg("force", force_value == "true")
            elif len(values) == 2 and values[1].startswith("-"):
                raise ValueError("zip-dl only supports the -force=true|false option")
            elif len(values) == 2:
                self.add_arg("destination", values[1])
                self.add_arg("write", True)

            require_nonempty_string(self.get_arg("source"), "source", "zip-dl")

    def _validate_json_arguments(self):
        require_nonempty_string(self.get_arg("source"), "source", "zip-dl")
        destination = self.get_arg("destination")
        if destination is not None:
            require_nonempty_string(destination, "destination", "zip-dl")

        force = self.get_arg("force")
        write = self.get_arg("write")
        if force is not None and not isinstance(force, bool):
            raise ValueError("zip-dl force must be a boolean")
        if write is not None and not isinstance(write, bool):
            raise ValueError("zip-dl write must be a boolean")
        if write is True and destination is None:
            raise ValueError("zip-dl write mode requires a destination")
        if force is True and write is True:
            raise ValueError("zip-dl force and write modes cannot both be enabled")

        if write is None:
            self.add_arg("write", destination is not None and force is not True)
        if force is None:
            self.add_arg("force", False)


class ZipDlCommand(CommandBase):
    cmd = "zip-dl"
    needs_admin = False
    help_cmd = """
Download a directory as a zip file in memory that's less than 1GB:
zip-dl C:\\Users\\checkymander\\secretstuff

Download a large directory as a zip file writing to disk first:
zip-dl C:\\Users\\checkymander\\secretstuff\\ C:\\Temp\\stage.zip

Download a directory as a zip file in memory that's larger than 1GB:
zip-dl C:\\Users\\checkymander\\secretstuff\\ -force=true

"""
    description = "Zip a directory and download it to Mythic"
    version = 1
    author = "@checkymander"
    argument_class = ZipDlArguments
    attackmapping = ["T1570"]
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