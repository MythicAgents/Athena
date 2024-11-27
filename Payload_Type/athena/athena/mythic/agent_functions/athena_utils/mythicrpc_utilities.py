from mythic_container.MythicCommandBase import *  # import the basics
import json  # import any other code you might need
# import the code for interacting with Files on the Mythic server
from mythic_container.MythicRPC import *

async def get_mythic_file(file_id):
    file = await SendMythicRPCFileGetContent(MythicRPCFileGetContentMessage(AgentFileId=file_id))

    if file.Success:
        return base64.b64encode(file.Content)
        # taskData.args.add_arg("asm", file_contents.decode("utf-8"))
        # taskData.args.remove_arg("file")
    else:
        raise Exception("Failed to get file contents: " + file.Error)
    
async def get_mythic_file_name(file_id):
    file_data = await SendMythicRPCFileSearch(MythicRPCFileSearchMessage(AgentFileID=file_id))
    if file_data.Success:
        return file_data.Files[0].Filename
    else:
        raise Exception("Failed to get file contents: " + file_data.Error)