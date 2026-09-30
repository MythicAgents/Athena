from mythic_container.MythicRPC import *
from mythic_container.MythicCommandBase import *
from .athena_utils.argument_utilities import load_json_or_get_shorthand


class SocksArguments(TaskArguments):
    def __init__(self, command_line, **kwargs):
        super().__init__(command_line, **kwargs)
        self.args = [
            CommandParameter(
                name="action",
                type=ParameterType.ChooseOne,
                choices=["start", "stop"],
                default_value="start",
                description="Start or stop the SOCKS5 proxy.",
            ),
            CommandParameter(
                name="port",
                type=ParameterType.Number,
                description="Port number on Mythic server for SOCKS5.",
            ),
        ]

    async def parse_arguments(self):
        command = load_json_or_get_shorthand(
            self, "socks", "Must specify start or stop and a port."
        )
        if command is not None:
            parts = command.split()
            if len(parts) == 1:
                action, port_text = "start", parts[0]
            elif len(parts) == 2:
                action, port_text = parts[0].lower(), parts[1]
            else:
                raise Exception("Usage: socks <start|stop> <port>")
            self.add_arg("action", action)
            try:
                self.add_arg("port", int(port_text))
            except ValueError:
                raise Exception("Invalid port number given: {}. Must be int.".format(port_text))

        action = (self.get_arg("action") or "start").lower()
        port = self.get_arg("port")
        if action not in ("start", "stop"):
            raise Exception("Action must be start or stop.")
        if isinstance(port, bool) or not isinstance(port, int) or not 1 <= port <= 65535:
            raise Exception("Port must be an integer from 1 through 65535.")
        self.add_arg("action", action)
        self.add_arg("port", port)


class SocksCommand(CommandBase):
    cmd = "socks"
    needs_admin = False
    help_cmd = "socks <start|stop> <port>"
    description = "Start or stop a SOCKS5 proxy on the Mythic server."
    version = 2
    author = "@checkymander"
    argument_class = SocksArguments
    attackmapping = ["T1090"]
    attributes = CommandAttributes(load_only=False, builtin=False)

    async def create_go_tasking(self, taskData: PTTaskMessageAllData) -> PTTaskCreateTaskingMessageResponse:
        action = taskData.args.get_arg("action")
        port = taskData.args.get_arg("port")
        if action == "stop":
            resp = await SendMythicRPCProxyStopCommand(MythicRPCProxyStopMessage(
                TaskID=taskData.Task.ID,
                PortType="socks",
                Port=port,
            ))
            display = "Stopped SOCKS5 server on port {}".format(port)
        else:
            resp = await SendMythicRPCProxyStartCommand(MythicRPCProxyStartMessage(
                TaskID=taskData.Task.ID,
                PortType="socks",
                LocalPort=port,
            ))
            display = "Started SOCKS5 server on port {}".format(port)

        response = PTTaskCreateTaskingMessageResponse(TaskID=taskData.Task.ID, Success=resp.Success)
        if not resp.Success:
            response.TaskStatus = MythicStatus.Error
            response.Stderr = resp.Error
            await SendMythicRPCResponseCreate(MythicRPCResponseCreateMessage(
                TaskID=taskData.Task.ID,
                Response=resp.Error.encode(),
            ))
        else:
            response.DisplayParams = display
            response.TaskStatus = MythicStatus.Success
            response.Completed = True
        return response

    async def process_response(self, task: PTTaskMessageAllData, response: any) -> PTTaskProcessResponseMessageResponse:
        return PTTaskProcessResponseMessageResponse(TaskID=task.Task.ID, Success=True)
