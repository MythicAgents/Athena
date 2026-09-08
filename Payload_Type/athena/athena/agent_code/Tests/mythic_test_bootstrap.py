import importlib.util
import json
import sys
import types
from pathlib import Path

COMMAND_DIR = Path(__file__).parents[2] / "mythic" / "agent_functions"


class TaskArguments:
    def __init__(self, command_line, **kwargs):
        self.command_line = command_line
        self.args = []
        self.types = {}

    def add_arg(self, name, value, parameter_type=None, **kwargs):
        for argument in self.args:
            if argument.name == name:
                argument.value = value
                argument.user_supplied = True
                if parameter_type is not None:
                    argument.type = parameter_type
                    self.types[name] = parameter_type
                return
        argument = CommandParameter(
            name=name, type=parameter_type or ParameterType.String, value=value
        )
        argument.user_supplied = True
        self.args.append(argument)
        self.types[name] = parameter_type or ParameterType.String

    def get_arg(self, name):
        for argument in self.args:
            if argument.name == name:
                return argument.value
        return None

    def load_args_from_json_string(self, value):
        try:
            parsed = json.loads(value)
            for key, item in parsed.items():
                for argument in self.args:
                    if argument.name == key or argument.cli_name == key:
                        argument.value = item
                        argument.user_supplied = True
        except Exception:
            # Mythic logs parser failures and returns without changing arguments.
            return

    def serialize(self):
        return json.dumps(
            {
                argument.name: argument.value
                for argument in self.args
                if argument.user_supplied
            },
            sort_keys=True,
        )


class CommandBase:
    pass


class StubObject:
    def __init__(self, *args, **kwargs):
        self.__dict__.update(kwargs)


class CommandParameter(StubObject):
    def __init__(self, *args, **kwargs):
        kwargs.setdefault("cli_name", kwargs.get("name"))
        kwargs.setdefault("value", kwargs.get("default_value"))
        kwargs.setdefault("user_supplied", False)
        super().__init__(*args, **kwargs)


class ParameterGroupInfo(StubObject):
    pass


class CommandAttributes(StubObject):
    pass


class ParameterType:
    String = "string"
    Boolean = "boolean"
    Number = "number"
    ChooseOne = "choose-one"
    File = "file"


class MythicStatus:
    Error = "error"
    Success = "success"


def install_mythic_stubs():
    base = types.ModuleType("mythic_container.MythicCommandBase")
    base_exports = (
        TaskArguments,
        CommandBase,
        CommandParameter,
        ParameterGroupInfo,
        ParameterType,
        CommandAttributes,
    )
    for value in base_exports:
        setattr(base, value.__name__, value)
    for name in (
        "PTTaskMessageAllData",
        "PTTaskCreateTaskingMessageResponse",
        "PTTaskProcessResponseMessageResponse",
        "AgentResponse",
    ):
        setattr(base, name, type(name, (StubObject,), {}))
    base.MythicCommandBase = base
    base.__all__ = [value.__name__ for value in base_exports] + [
        "PTTaskMessageAllData",
        "PTTaskCreateTaskingMessageResponse",
        "PTTaskProcessResponseMessageResponse",
        "AgentResponse",
        "MythicCommandBase",
    ]

    rpc = types.ModuleType("mythic_container.MythicRPC")
    rpc_types = (
        "PTTaskMessageAllData",
        "PTTaskCreateTaskingMessageResponse",
        "PTTaskProcessResponseMessageResponse",
        "MythicRPCProxyStartMessage",
        "MythicRPCProxyStopMessage",
        "MythicRPCResponseCreateMessage",
    )
    for name in rpc_types:
        setattr(rpc, name, type(name, (StubObject,), {}))
    rpc.MythicStatus = MythicStatus

    async def rpc_stub(*args, **kwargs):
        return StubObject(Success=True, Error="")

    rpc_functions = (
        "SendMythicRPCProxyStartCommand",
        "SendMythicRPCProxyStopCommand",
        "SendMythicRPCResponseCreate",
    )
    for name in rpc_functions:
        setattr(rpc, name, rpc_stub)
    rpc.__all__ = [*rpc_types, *rpc_functions, "MythicStatus"]

    sys.modules.update(
        {
            "mythic_container": types.ModuleType("mythic_container"),
            "mythic_container.MythicCommandBase": base,
            "mythic_container.MythicRPC": rpc,
        }
    )


def load_command(filename, package_name="athena_test_agent_functions"):
    install_mythic_stubs()
    if package_name not in sys.modules:
        package = types.ModuleType(package_name)
        package.__path__ = [str(COMMAND_DIR)]
        sys.modules[package_name] = package

    module_name = package_name + "." + filename.removesuffix(".py").replace("-", "_")
    spec = importlib.util.spec_from_file_location(module_name, COMMAND_DIR / filename)
    if spec is None or spec.loader is None:
        raise ImportError("Unable to load {}".format(filename))
    module = importlib.util.module_from_spec(spec)
    sys.modules[module_name] = module
    spec.loader.exec_module(module)
    return module
