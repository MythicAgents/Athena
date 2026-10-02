from __future__ import annotations

import base64
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *



async def create_subtask_or_raise(message):
    result = await SendMythicRPCTaskCreateSubtask(message)
    if not result.Success:
        raise Exception(f"Failed to create subtask: {result.Error}")
    return result


async def get_mythic_file(file_id: str) -> str:
    file = await SendMythicRPCFileGetContent(
        MythicRPCFileGetContentMessage(AgentFileId=file_id)
    )
    if not file.Success:
        raise Exception("Failed to get file contents: " + file.Error)
    return base64.b64encode(file.Content).decode("utf-8")


async def get_mythic_file_name(file_id: str) -> str:
    file_data = await SendMythicRPCFileSearch(
        MythicRPCFileSearchMessage(AgentFileID=file_id)
    )
    if not file_data.Success:
        raise Exception("Failed to get file contents: " + file_data.Error)
    if not file_data.Files:
        raise Exception(f"File with ID: {file_id} not found.")
    return file_data.Files[0].Filename


async def create_mythic_file(
    task_id: str, file_contents, file_name: str, delete_after_fetch: bool
) -> MythicRPCFileCreateMessageResponse:
    file_create = MythicRPCFileCreateMessage(
        task_id,
        DeleteAfterFetch=delete_after_fetch,
        FileContents=file_contents,
        Filename=file_name,
    )
    response = await SendMythicRPCFileCreate(file_create)
    if not response.Success:
        raise Exception("Failed to create file: " + response.Error)
    return response


async def forward_subtask_responses(
    completion_msg: PTTaskCompletionFunctionMessage, transform=None
) -> PTTaskCompletionFunctionMessageResponse:
    responses = await SendMythicRPCResponseSearch(
        MythicRPCResponseSearchMessage(TaskID=completion_msg.SubtaskData.Task.ID)
    )
    combined = "".join(str(output.Response) for output in responses.Responses)
    output_text = transform(combined) if transform is not None else combined
    await SendMythicRPCResponseCreate(
        MythicRPCResponseCreateMessage(
            TaskID=completion_msg.TaskData.Task.ID,
            Response=f"{output_text}",
        )
    )
    return PTTaskCompletionFunctionMessageResponse(
        Success=True, TaskStatus="success", Completed=True
    )