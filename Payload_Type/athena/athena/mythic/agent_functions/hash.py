from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

class HashArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action", cli_name="action",
                display_name="Action",
                type=ParameterType.ChooseOne,
                choices=["hash", "base64"],
                default_value="hash",
                description="Operation mode",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=0)
                ]
            ),
            CommandParameter(
                name="path", cli_name="path",
                display_name="File Path",
                type=ParameterType.String,
                description="File to hash or encode",
                parameter_group_info=[
                    ParameterGroupInfo(required=True, group_name="Default", ui_position=1)
                ]
            ),
            CommandParameter(
                name="algorithm", cli_name="algorithm",
                display_name="Algorithm",
                type=ParameterType.ChooseOne,
                choices=["md5", "sha1", "sha256", "sha384", "sha512"],
                default_value="sha256",
                description="Hash algorithm (for hash action)",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=2)
                ]
            ),
            CommandParameter(
                name="encode", cli_name="encode",
                display_name="Encode",
                type=ParameterType.Boolean,
                default_value=True,
                description="True to encode, False to decode (for base64 action)",
                parameter_group_info=[
                    ParameterGroupInfo(required=False, group_name="Default", ui_position=3)
                ]
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

class HashCommand(CommandBase):
    cmd = "hash"
    needs_admin = False
    depends_on = None
    plugin_libraries = []
    help_cmd = "hash -path /etc/passwd -algorithm sha256"
    description = "Compute file hashes (MD5/SHA1/SHA256/SHA384/SHA512) or base64 encode/decode"
    version = 1
    author = "@checkymander"
    argument_class = HashArguments
    attackmapping = ["T1005"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID, Success=True)
        action = taskData.args.get_arg("action")
        path = taskData.args.get_arg("path")
        if action == "hash":
            algo = taskData.args.get_arg("algorithm")
            response.DisplayParams = f"{algo} {path}"
        else:
            response.DisplayParams = f"base64 {path}"
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
