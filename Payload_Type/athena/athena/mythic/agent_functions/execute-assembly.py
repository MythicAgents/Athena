from mythic_container.MythicCommandBase import *  # import the basics
from .athena_utils import message_utilities
from .athena_utils.mythicrpc_utilities import *
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
from .athena_utils import message_converter

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class ExecuteAssemblyArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="file",
                type=ParameterType.File,
                parameter_group_info=[ParameterGroupInfo(ui_position=0)],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1, 
                        required=False
                    )],
            )
        ]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class ExecuteAssemblyCommand(CommandBase):
    cmd = "execute-assembly"
    needs_admin = False
    help_cmd = "execute-assembly"
    description = "Load an arbitrary .NET assembly via Assembly.Load and track the assembly FullName to call for execution with the runassembly command."
    version = 1
    author = ""
    argument_class = ExecuteAssemblyArguments
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

        file_contents = await get_mythic_file(taskData.args.get_arg("file"))
        original_file_name = await get_mythic_file_name(taskData.args.get_arg("file"))
        taskData.args.add_arg("asm", file_contents)

        if taskData.args.get_arg("arguments") is None:
            taskData.args.add_arg("arguments", "")
        
        response.DisplayParams = "{} {}".format(
            original_file_name, 
            taskData.args.get_arg("arguments"))
        return response


    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
