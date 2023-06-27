from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

async def send_agent_message(message, task: MythicTask):
    await MythicRPC().execute("create_output", task_id=task.id, output=message)
    resp = PTTaskProcessResponseMessageResponse(TaskID=task.id, Success=True)