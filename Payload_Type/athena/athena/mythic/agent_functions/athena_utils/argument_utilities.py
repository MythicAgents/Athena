import shlex


def load_json_or_get_shorthand(arguments, command_name, empty_message=None):
    """Load a JSON object or return normalized shorthand for command grammar."""
    command_line = arguments.command_line.strip()
    if not command_line:
        raise ValueError(empty_message or "{} requires arguments".format(command_name))
    if command_line.startswith("{"):
        arguments.load_args_from_json_string(command_line)
        return None
    return command_line


def split_shorthand(command_line, command_name):
    """Split quote-aware command input without consuming Windows backslashes."""
    try:
        values = shlex.split(command_line, posix=False)
    except ValueError as error:
        raise ValueError("Invalid {} arguments: {}".format(command_name, error)) from error

    return [
        value[1:-1]
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'"
        else value
        for value in values
    ]


def require_nonempty_string(value, name, command_name):
    if not isinstance(value, str) or not value.strip():
        raise ValueError("{} requires a nonempty {}".format(command_name, name))
