from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *
from .athena_utils.mythicrpc_utilities import *

class NslookupArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="hosts",
                type=ParameterType.String,
                description="Comma separate list of hosts",
                parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    group_name="Default"
                )]
            ),
            CommandParameter(
                name="inputlist",
                type=ParameterType.File,
                description="List of hosts in a newline separated file",
                parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    group_name="TargetList"
                )]
            )
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hosts", self.command_line)
        else:
            raise ValueError("Missing arguments")


class NsLookupCommand(CommandBase):
    cmd = "nslookup"
    needs_admin = False
    help_cmd = "nslookup DC1.gaia.local,FS1.gaia.local,gaia.local"
    description = "Perform an nslookup on the provided hosts"
    version = 1
    author = "@checkymander"
    argument_class = NslookupArguments
    attackmapping = ["T1106"]
    attributes = CommandAttributes()

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )
        groupName = taskData.args.get_parameter_group_name()

        if groupName == "TargetList":
            encoded_file_contents = await get_mythic_file(taskData.args.get_arg("inputlist"))
            original_file_name = await get_mythic_file_name(taskData.args.get_arg("inputlist"))
            taskData.args.add_arg("targetlist", encoded_file_contents, parameter_group_info=[ParameterGroupInfo(
                    required=True,
                    group_name="TargetList"
                )])
            response.DisplayParams = original_file_name
        else:
            response.DisplayParams = taskData.args.get_arg("hosts")  
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
