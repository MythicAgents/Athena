from mythic_payloadtype_container.MythicCommandBase import *
import json


class KillArguments(TaskArguments):

    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="id",
                cli_name="id",
                display_name="id",
                type=ParameterType.Number)
        ]

    async def parse_arguments(self):
        if len(self.command_line) == 0:
            raise Exception("No id given.")
        if self.command_line[0] == "{":
            self.load_args_from_json_string(self.command_line)
        else:
            try:
                int(self.command_line)
            except:
                raise Exception("Failed to parse integer id from: {}\n\tUsage: {}".format(self.command_line, killCommand.help_cmd))
            self.add_arg("pid", int(self.command_line), ParameterType.Number)
        

class killCommand(CommandBase):
    cmd = "kill"
    needs_admin = False
    help_cmd = "kill [id]"
    description = "Kill a process specified by an ID"
    version = 1
    is_exit = False
    is_file_browse = False
    is_process_list = False
    is_download_file = False
    is_upload_file = False
    is_remove_file = False
    author = "@checkymander"
    argument_class = KillArguments
    attackmapping = ["T1106"]
    supported_ui_features = ["kill"]
    attributes = CommandAttributes(
    )


    async def create_tasking(self, task: MythicTask) -> MythicTask:
        task.display_params = " {}".format(task.args.get_arg("id"))
        return task

    async def process_response(self, response: AgentResponse):
        pass