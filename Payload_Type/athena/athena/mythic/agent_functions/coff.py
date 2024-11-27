from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *
import base64
from .athena_utils import message_converter
from .athena_utils.bof_utilities import *

# create a class that extends TaskArguments class that will supply all the arguments needed for this command
class CoffArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        # this is the part where you'd add in your additional tasking parameters
        self.args = [
            CommandParameter(
                name="coffFile",
                type=ParameterType.File,
                description="Upload COFF file to be executed (typically ends in .o)",
                parameter_group_info=[
                    ParameterGroupInfo(
                        required=True,
                        ui_position=0,
                        group_name="Default"
                        ),
                        ParameterGroupInfo(
                        ui_position=0,
                        required=True,
                        group_name="Argument String"
                        ),
                    ],
            ),
            CommandParameter(
                name="functionName",
                type=ParameterType.String,
                description="Name of entry function to execute in COFF",
                default_value="go",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        group_name="Default"
                        ),
                        ParameterGroupInfo(
                        ui_position=1,
                        required=True,
                        group_name="Argument String"
                        ),
                    ],
            ),
            CommandParameter(
                name="arguments",
                type=ParameterType.String,
                description="Arguments converted to bytes using beacon_compatibility.py",
                default_value="",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=False,
                        group_name="Argument String"
                        ),
                    ],
            ),
            CommandParameter(
                name="argument_array",
                type=ParameterType.TypedArray,
                choices=["int16", "int32", "string", "wchar", "base64"],
                description="""Arguments to pass to the COFF via the following way:
                -s:123 or int16:123
                -i:123 or int32:123
                -z:hello or string:hello
                -Z:hello or wchar:hello
                -b:SGVsbG9Xb3JsZA== or base64:SGVsbG9Xb3JsZA==""",
                typedarray_parse_function=self.get_arguments,
                default_value=[],
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=2,
                        required=False,
                        group_name="Default"
                        ),
                    ],
            ),
            CommandParameter(
                name="timeout",
                type=ParameterType.String,
                description="Time to wait for the coff file to execute before killing it",
                default_value="30",
                parameter_group_info=[
                    ParameterGroupInfo(
                        ui_position=3,
                        required=False,
                        group_name="Default"
                        ),
                        ParameterGroupInfo(
                        ui_position=3,
                        required=True,
                        group_name="Argument String"
                        ),
                    ],
            ),
        ]

    # you must implement this function so that you can parse out user typed input into your paramters or load your parameters based on some JSON input
    async def parse_arguments(self):
        if len(self.command_line) > 0:
            if self.command_line[0] == "{":
                self.load_args_from_json_string(self.command_line)

    async def get_arguments(self, arguments: PTRPCTypedArrayParseFunctionMessage) -> PTRPCTypedArrayParseFunctionMessageResponse:
        argumentResponse = PTRPCTypedArrayParseFunctionMessageResponse(Success=True)
        argumentSplitArray = []
        for argValue in arguments.InputArray:
            argSplitResult = argValue.split(" ")
            for spaceSplitArg in argSplitResult:
                argumentSplitArray.append(spaceSplitArg)
        coff_arguments = []
        for argument in argumentSplitArray:
            argType,value = argument.split(":",1)
            value = value.strip("\'").strip("\"")
            if argType == "":
                pass
            elif argType == "int16" or argType == "-s":
                coff_arguments.append(["int16",int(value)])
            elif argType == "int32" or argType == "-i":
                coff_arguments.append(["int32",int(value)])
            elif argType == "string" or argType == "-z":
                coff_arguments.append(["string",value])
            elif argType == "wchar" or argType == "-Z":
                coff_arguments.append(["wchar",value])
            elif argType == "base64" or argType == "-b":
                coff_arguments.append(["base64",value])
            else:
                return PTRPCTypedArrayParseFunctionMessageResponse(Success=False, Error=f"Failed to parse argument: {argument}: Unknown value type.")

        argumentResponse = PTRPCTypedArrayParseFunctionMessageResponse(Success=True, TypedArray=coff_arguments)
        return argumentResponse


# this is information about the command itself
class CoffCommand(CommandBase):
    cmd = "coff"
    needs_admin = False
    help_cmd = "coff"
    description = "Execute a COFF file in process. Leverages the Netitude RunOF project. argumentData can be generated using the beacon_generate.py script found in the TrustedSec COFFLoader GitHub repo. This command is not intended to be used directly, but can be."
    version = 1
    author = "@checkymander & @scottctaylor12"
    argument_class = CoffArguments
    attackmapping = ["T1620"]
    attributes = CommandAttributes(
        load_only=False,
        builtin=False,
        supported_os=[SupportedOS.Windows],
    )

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        response = PTTaskCreateTaskingMessageResponse(
            TaskID=taskData.Task.ID,
            Success=True,
        )

        file = await SendMythicRPCFileGetContent(MythicRPCFileGetContentMessage(AgentFileId=taskData.args.get_arg("coffFile")))
        if file.Success:
            file_contents = base64.b64encode(file.Content)
            decoded_buffer = base64.b64decode(file_contents)
            taskData.args.add_arg("fileSize", f"{len(decoded_buffer)}", parameter_groupInfo=taskData.args.parameter_group_name)
            taskData.args.add_arg("asm", file_contents.decode("utf-8"), parameter_groupInfo=taskData.args.parameter_group_name)
        else:
            raise Exception("Failed to get file contents: " + file.Error)
        
        #There's no way this will fail since we're searching for the same file we just confirmed exists
        file_data = await SendMythicRPCFileSearch(MythicRPCFileSearchMessage(AgentFileID=taskData.args.get_arg("coffFile"))) 
        original_file_name = file_data.Files[0].Filename

        if(taskData.args.parameter_group_name != "Argument String"):
            taskargs = taskData.args.get_arg("argument_array")
            if taskargs == "" or taskargs is None:
                taskData.args.add_arg("arguments", "", parameter_groupInfo=taskData.args.parameter_group_name)
            else:
                OfArgs = []    
                for type_array in taskargs:
                    if type_array[0] == "int16":
                        OfArgs.append(generate16bitInt(type_array[1]))
                    if type_array[0] == "int32":
                        OfArgs.append(generate32bitInt(type_array[1]))
                    if type_array[0] == "string":
                        OfArgs.append(generateString(type_array[1]))
                    if type_array[0] == "wchar":
                        OfArgs.append(generateWString(type_array[1]))
                    if type_array[0] == "base64":
                        OfArgs.append(generateBinary(type_array[1]))

                encoded_args = base64.b64encode(SerializeArgs(OfArgs)).decode("utf-8")
                taskData.args.add_arg("arguments", encoded_args, parameter_groupInfo=taskData.args.parameter_group_name)
            
            #Remove argument_array because we don't need it anymore
            taskData.args.remove_arg("argument_array")

        response.DisplayParams = "-coffFile {} -functionName {}-timeout {} -arguments {}".format(
            original_file_name,
            taskData.args.get_arg("functionName"),
            encoded_args,
            taskData.args.get_arg("timeout")
        )

        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        if "message" in response:
            user_output = response["message"]
            await MythicRPC().execute("create_output", task_id=task.Task.ID, output=message_converter.translateAthenaMessage(user_output))

        resp = PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
        return resp
