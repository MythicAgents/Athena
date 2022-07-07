from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class PowerShellScriptArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = [
            CommandParameter(
                name="Arguments",
                type=ParameterType.String,
                description="script argument",
                parameter_group_info=[ParameterGroupInfo(ui_position=2, required=False)],
            ),
            CommandParameter(
                name="File",
                type=ParameterType.File,
                description="or powershell script to be executed",
                parameter_group_info=[ParameterGroupInfo(ui_position=1,required=True)],
            ),
            CommandParameter(
                name="Additional-Command",
                type=ParameterType.String,
                description="Command to be executed after the script",
                parameter_group_info=[ParameterGroupInfo(ui_position=2, required=False)],
            )
        ]
    async def parse_arguments(self):
        if len(self.command_line.strip()) == 0:
            raise Exception("powershell requires at least one command-line parameter.\n\tUsage: {}".format(PowerShellCommand.help_cmd))
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            self.args["path"].value = self.command_line
         



class PowerShellScriptCommand(CommandBase):
    cmd = "powershell-script"
    needs_admin = False
    help_cmd = "powershell-script [script] [script_arguments] [additionnal command]"
    description = "Run a powershell script in the agent process"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = True
    is_upload_file = False
    is_remove_file = False
    author = "@ascemama"
    argument_class = PowerShellScriptArguments
    attackmapping = ["T1059", "T1059.004"]
    attributes = CommandAttributes(
        load_only=True,
    )

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        if task.args.get_arg("File"):
            file_resp = await MythicRPC().execute("get_file",file_id=task.args.get_arg("File"),task_id=task.id,get_contents=True)
            if file_resp.status == MythicRPCStatus.Success:
                if len(file_resp.response) > 0:
                    task.args.add_arg("ps1", file_resp.response[0]["contents"])
                    task.display_params = f"{file_resp.response[0]['filename']}"
                else:
                    raise Exception("Failed to find that file")
            else:
                raise Exception("Error from Mythic trying to get file: " + str(file_resp.error))

        return task

    async def process_response(self, response: AgentResponse):
        pass
