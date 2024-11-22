from mythic_container.MythicCommandBase import *  # import the basics
from .athena_utils import message_utilities
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
from .athena_utils import message_converter

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class ExecuteModuleArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="file",
                type=ParameterType.File,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0
                    )            
                    ],
            ),
            CommandParameter(
                name="name",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1, 
                        required=True,
                        group_name="Default"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        group_name="Existing Module",
                        ui_position=1
                    )],
            ),
            CommandParameter(
                name="entrypoint",
                type=ParameterType.String,
                default_value = "Execute",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2, 
                        required=True,
                        group_name="Default"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        group_name="Existing Module",
                        ui_position=2
                    )],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=3, 
                        required=True,
                        group_name="Default"
                    ),
                    ParameterGroupInfo(
                        required=True,
                        group_name="Existing Module",
                        ui_position=3
                    )],
            )
        ]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class ExecuteModuleCommand(CommandBase):
    cmd = "execute-module"
    needs_admin = False
    help_cmd = "execute-module"
    description = "Load a supported Athena module in memory and execute the function"
    version = 1
    author = ""
    argument_class = ExecuteModuleArguments
    attackmapping = ["T1620"]
    browser_script = None
    attributes = CommandAttributes(
        load_only=False,
        builtin=False
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )

        if taskData.args.parameter_group_name == "Existing Module":
            response.DisplayParams = "{} {}".format(taskData.args.get_arg("name"), taskData.args.get_arg("arguments"))
        else:
            file_data = await SendMythicRPCFileSearch(MythicRPCFileSearchMessage(AgentFileID=taskData.args.get_arg("file")))      
            if file_data.Success:
                original_file_name = file_data.Files[0].Filename
                response.DisplayParams = "{}.{} {}".format(original_file_name, taskData.args.get_arg("name"), taskData.args.get_arg("arguments"))
    
        return response


    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
