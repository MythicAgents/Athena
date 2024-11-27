from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *

async def get_mythic_file(file_id: str) -> str:
    file = await SendMythicRPCFileGetContent(MythicRPCFileGetContentMessage(AgentFileId=file_id))

    if not file.Success:
        raise Exception("Failed to get file contents: " + file.Error)
    
    return base64.b64encode(file.Content).decode("utf-8")
    
async def get_mythic_file_name(file_id: str) -> str:
    file_data = await SendMythicRPCFileSearch(MythicRPCFileSearchMessage(AgentFileID=file_id))

    if not file_data.Success:
        raise Exception("Failed to get file contents: " + file_data.Error)
    
    if len(file_data.Files) == 0:
        raise Exception(f"File with ID: {file_id} not found.")
    
    return file_data.Files[0].Filename

async def create_mythic_file(task_id: str, file_contents, file_name: str, delete_after_fetch: bool) -> MythicRPCFileCreateMessageResponse:
        fileCreate = MythicRPCFileCreateMessage(task_id, DeleteAfterFetch = delete_after_fetch, FileContents = file_contents, Filename = file_name)
        fileCreateRPC = await SendMythicRPCFileCreate(fileCreate)

        if not fileCreateRPC.Success:
            raise Exception("Failed to create file: " + fileCreateRPC.Error)

        return fileCreateRPC