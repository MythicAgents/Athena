from __future__ import annotations

from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *



async def send_agent_message(message, task: PTTaskMessageTaskData):
    await MythicRPC().execute("create_output", task_id=task.ID, output=message)
    return PTTaskProcessResponseMessageResponse(TaskID=task.ID, Success=True)