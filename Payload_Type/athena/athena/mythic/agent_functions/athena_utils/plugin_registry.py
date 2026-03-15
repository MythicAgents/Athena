from mythic_container.MythicCommandBase import CommandBase

_command_registry: dict[str, type] = {}
_subcommand_cache: dict[str, list[str]] = {}
_initialized = False


def _ensure_initialized():
    global _initialized, _command_registry, _subcommand_cache
    if _initialized:
        return

    def _collect_subclasses(cls):
        for sub in cls.__subclasses__():
            if hasattr(sub, 'cmd'):
                _command_registry[sub.cmd] = sub
            _collect_subclasses(sub)

    _collect_subclasses(CommandBase)

    for cmd_name, cmd_cls in _command_registry.items():
        parent = getattr(cmd_cls, 'depends_on', None)
        if parent:
            _subcommand_cache.setdefault(parent, []).append(cmd_name)

    _initialized = True


def get_subcommands(plugin_name: str) -> list[str]:
    _ensure_initialized()
    return _subcommand_cache.get(plugin_name, [])


def get_libraries(command_name: str) -> list[dict[str, str]]:
    _ensure_initialized()
    cmd_cls = _command_registry.get(command_name)
    if not cmd_cls:
        return []
    libs = getattr(cmd_cls, 'plugin_libraries', [])
    return [{"libraryname": lib, "target": "plugin"} for lib in libs]


def is_subcommand(command_name: str) -> bool:
    _ensure_initialized()
    cmd_cls = _command_registry.get(command_name)
    if not cmd_cls:
        return False
    return getattr(cmd_cls, 'depends_on', None) is not None


def get_parent(command_name: str) -> str | None:
    _ensure_initialized()
    cmd_cls = _command_registry.get(command_name)
    if not cmd_cls:
        return None
    return getattr(cmd_cls, 'depends_on', None)


def get_all_subcommands() -> list[str]:
    _ensure_initialized()
    return [
        name for name, cls in _command_registry.items()
        if getattr(cls, 'depends_on', None) is not None
    ]


def reset():
    global _initialized, _command_registry, _subcommand_cache
    _initialized = False
    _command_registry = {}
    _subcommand_cache = {}
