import struct
import subprocess
import os
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

from .mythicrpc_utilities import *

class OfArg:
    def __init__(self, arg_data, arg_type):
        self.arg_data = arg_data
        self.arg_type = arg_type

def generateWString(arg):
    return OfArg(arg.encode('utf-16le') + b'\x00\x00', 0)

def generateString(arg):
    return OfArg(arg.encode('ascii') + b'\x00', 0)

def generate32bitInt(arg):
    return OfArg(struct.pack('<I', int(arg)), 1)

def generate16bitInt(arg):
    return OfArg(struct.pack('<H', int(arg)), 2)

def generateBinary(arg):
    return OfArg(arg, 0)

def SerializeArgs(OfArgs):
    output_bytes = b''
    for of_arg in OfArgs:
        output_bytes += struct.pack('<I', of_arg.arg_type)
        output_bytes += struct.pack('<I', len(of_arg.arg_data))
        output_bytes += of_arg.arg_data
    return output_bytes

async def compile_bof(bof_path):
    p = subprocess.Popen(["make"], cwd=bof_path)
    p.wait()
    streamdata = p.communicate()[0]
    rc = p.returncode
    if rc != 0:
        raise Exception("Error compiling BOF: " + str(streamdata))

async def compile_and_upload_bof_to_mythic( task_id: str, bof_folder: str, bof_name: str):
        full_path = os.path.join("/Mythic/athena/mythic/agent_functions/", bof_folder,f"{bof_name}")
        folder_path = os.path.join("/Mythic/athena/mythic/agent_functions/",bof_folder)

        if not os.path.isdir(folder_path):
            raise Exception("Folder not found: " + folder_path)

        if not os.path.isfile(full_path):
            await compile_bof(folder_path)
            if not os.path.isfile(full_path):
                raise Exception("Failed to compile bof!")
        
        with open(full_path, "rb") as f:
            coff_file = f.read()
        
        file_resp = await create_mythic_file(task_id, coff_file, bof_name, True)

        if not file_resp.Success:
            raise Exception("Failed to upload bof: " + file_resp.Error)
        
        return file_resp.AgentFileId

# This function merge the output of the subtasks and mark the parent task as completed.
async def default_coff_completion_callback(completionMsg: PTTaskCompletionFunctionMessage) -> PTTaskCompletionFunctionMessageResponse:
    out = ""
    response = PTTaskCompletionFunctionMessageResponse(Success=True, TaskStatus="success", Completed=True)
    responses = await SendMythicRPCResponseSearch(MythicRPCResponseSearchMessage(TaskID=completionMsg.SubtaskData.Task.ID))
    for output in responses.Responses:
        out += str(output.Response)
            
    await SendMythicRPCResponseCreate(MythicRPCResponseCreateMessage(
        TaskID=completionMsg.TaskData.Task.ID,
        Response=f"{out}"
    ))
    return response

class CoffCommandBase(CommandBase):
    completion_functions = {"coff_completion_callback": default_coff_completion_callback}
    