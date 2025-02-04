from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *
from .athena_utils.mythicrpc_utilities import *

class TestportArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="hosts",
                type=ParameterType.String,
                description="The hosts to check (comma separated)",
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default"
                    )],
            ),            
            CommandParameter(
                name="inputlist",
                type=ParameterType.File,
                description="List of hosts in a newline separated file",
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        group_name="TargetList",
                        ui_position = 0
                )]
            ),
            CommandParameter(
                name="ports",
                type=ParameterType.String,
                description="TCP ports to check (comma separated)",
                parameter_group_info=[ParameterGroupInfo(
                        required=True,
                        ui_position=1,
                        group_name="Default"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        ui_position=1,
                        group_name="TargetList"
                    )
                ],
            ),
        ]

    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)
            else:
                self.add_arg("hosts", self.command_line.split()[0])
                self.add_arg("ports", self.command_line.split()[1])
        else:
            raise ValueError("Missing arguments")


class TestportCommand(CommandBase):
    cmd = "test-port"
    needs_admin = False
    help_cmd = "test-port"
    description = "Check if a list of ports are open against a host/list of hosts."
    version = 1
    author = "@checkymander"
    argument_class = TestportArguments
    attackmapping = ["T1046","T1595"]
    attributes = CommandAttributes(
    )
    
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
            response.DisplayParams = f"{taskData.args.get_arg('hosts')} on ports {taskData.args.get_arg('ports')}"  
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass
