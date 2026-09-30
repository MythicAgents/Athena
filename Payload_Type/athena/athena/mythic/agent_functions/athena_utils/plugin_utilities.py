from __future__ import annotations

import base64
import json
from datetime import datetime, timedelta
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

from .mythicrpc_utilities import *


def get_coff_commands():
    return [
            "adcs-enum",
            "add-machine-account",
            "add-user-to-group",
            "ask-creds",
            "delete-machine-account",
            "driver-sigs",
            "enable-user",
            "get-machine-account-quota",
            "get-password-policy",
            "kerberoast",
            "klist", 
            "nanorubeus", 
            "net-view",
            "office-tokens",
            "patchit",
            "schtasks-create", 
            "schtasks-delete",
            "schtasks-enum",
            "schtasks-query",
            "schtasks-run", 
            "schtasks-stop",
            "set-user-pass",
            "sc-config",
            "sc-create",
            "sc-delete",
            "sc-enum", 
            "sc-start",
            "sc-stop",
            "vss-enum",
            "windowlist",
            "wmi-query",
            ]

def get_inject_shellcode_commands():
    return ["inject-assembly"]

def get_ds_commands():
    return ["ds-query", "ds-connect"]

def get_builtin_commands():
    return ["load", "load-assembly"]

def get_unloadable_commands():
    return get_ds_commands() + get_coff_commands() + get_inject_shellcode_commands() + get_nidhogg_commands() + get_builtin_commands()

def get_nidhogg_commands():
    return ["nidhogg-disableetwti", 
            "nidhogg-dumpcreds", 
            "nidhogg-elevateprocess",
            "nidhogg-enableetwti", 
            "nidhogg-hidedriver", 
            "nidhogg-hideport", 
            "nidhogg-hideprocess", 
            "nidhogg-hideregistrykey",
            "nidhogg-hideregistryvalue",
            "nidhogg-hidethread", 
            "nidhogg-injectdll", 
            "nidhogg-protectfile", 
            "nidhogg-protectprocess",
            "nidhogg-protectregistrykey", 
            "nidhogg-protectregistryvalue",
            "nidhogg-protectthread", 
            "nidhogg-unhidedriver", 
            "nidhogg-unhideport", 
            "nidhogg-unhideregistrykey", 
            "nidhogg-unhideregistryvalue", 
            "nidhogg-unhidethread", 
            "nidhogg-unprotectfile", 
            "nidhogg-unprotectprocess", 
            "nidhogg-unprotectregistrykey", 
            "nidhogg-unprotectregistryvalue", 
            "nidhogg-unprotectthread"]

FILETIME_ATTRIBUTES = frozenset(
    ("pwdlastset", "lastlogontimestamp", "lastlogon", "badpasswordtime", "accountexpires")
)
MEMBER_ATTRIBUTES = frozenset(("memberof", "member"))
TIMESTAMP_ATTRIBUTES = frozenset(
    ("whencreated", "whenchanged", "dscorepropagationdata")
)


# This function merges the output of the subtasks and marks the parent task as completed.
async def default_ldap_completion_callback(
    completionMsg: PTTaskCompletionFunctionMessage,
) -> PTTaskCompletionFunctionMessageResponse:
    return await forward_subtask_responses(completionMsg, decode_ldap)


def _decode_ldap_guid(encoded: str) -> str:
    raw = base64.b64decode(encoded)
    guid = "".join(f"{byte:02x}" for byte in raw)
    return (
        f"{guid[6:8]}{guid[4:6]}{guid[2:4]}{guid[0:2]}-"
        f"{guid[10:12]}{guid[8:10]}-{guid[14:16]}{guid[12:14]}-"
        f"{guid[16:20]}-{guid[20:]}"
    )


def _decode_ldap_sid(encoded: str) -> str:
    buf = list(base64.b64decode(encoded))
    version = buf[0]
    sub_authority_count = buf[1]
    identifier_authority = int("".join(hex(buf[i])[2:] for i in range(2, 8)), 16)
    sid_parts = [f"S-{version}-{identifier_authority}"]
    for i in range(sub_authority_count):
        offset = 8 + i * 4
        sub_auth = int(
            "".join(f"{buf[offset + j]:02x}" for j in range(3, -1, -1)),
            16,
        )
        sid_parts.append(str(sub_auth))
    return "-".join(sid_parts)


def _decode_ldap_filetime(encoded: str) -> str:
    raw = base64.b64decode(encoded)
    decimal_value = int.from_bytes(raw[:4], "little")
    date = datetime(1601, 1, 1) + timedelta(microseconds=(decimal_value / 1e4))
    return date.isoformat()


def _decode_ldap_members(values: list[str]) -> str:
    lines = ["\n"]
    for attr in values:
        try:
            decoded = base64.b64decode(attr).decode("utf-8")
            lines.append(f"\n\t\t{decoded} ")
        except UnicodeDecodeError:
            lines.append("\n\t\t(binary data) ")
    return "".join(lines)


def _decode_ldap_timestamps(values: list[str]) -> str:
    parts = []
    for attr in values:
        try:
            timestamp = base64.b64decode(attr).decode("utf-8").split(".")[0]
            date = datetime(
                int(timestamp[:4]),
                int(timestamp[4:6]),
                int(timestamp[6:8]),
                int(timestamp[8:10]),
                int(timestamp[10:12]),
                int(timestamp[12:14]),
            )
            parts.append(f"\n\t\t{date.isoformat()} ")
        except UnicodeDecodeError:
            parts.append("\n\t\t(binary timestamp data) ")
    return "".join(parts)


def _decode_ldap_default(values: list[str]) -> str:
    parts = []
    for attr in values:
        try:
            parts.append(base64.b64decode(attr).decode("utf-8") + " ")
        except UnicodeDecodeError:
            parts.append("(binary data) ")
    return "".join(parts)


def _format_ldap_attribute(key: str, attribute: list[str]) -> str:
    if key == "objectguid":
        return _decode_ldap_guid(attribute[0])
    if key == "objectsid":
        return _decode_ldap_sid(attribute[0])
    if key in FILETIME_ATTRIBUTES:
        return _decode_ldap_filetime(attribute[0])
    if key in MEMBER_ATTRIBUTES:
        return _decode_ldap_members(attribute)
    if key in TIMESTAMP_ATTRIBUTES:
        return _decode_ldap_timestamps(attribute)
    return _decode_ldap_default(attribute)


def decode_ldap(json_string):
    data = json.loads(json_string)
    lines = []
    for item in data:
        lines.append(item["DistinguishedName"] + "\n")
        for key, attribute in item["Attributes"].items():
            value = _format_ldap_attribute(key, attribute)
            lines.append(f"    {key}: {value}\n")
        lines.append("\n")
    return "".join(lines)


async def default_completion_callback(
    completionMsg: PTTaskCompletionFunctionMessage,
) -> PTTaskCompletionFunctionMessageResponse:
    return await forward_subtask_responses(completionMsg)