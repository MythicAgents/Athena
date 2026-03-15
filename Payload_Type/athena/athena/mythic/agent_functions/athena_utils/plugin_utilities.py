import base64
import json
from datetime import datetime, timedelta
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *

from .mythicrpc_utilities import *

def get_builtin_commands():
    return ["load", "load-assembly"]

# This function merge the output of the subtasks and mark the parent task as completed.
async def default_ldap_completion_callback(completionMsg: PTTaskCompletionFunctionMessage) -> PTTaskCompletionFunctionMessageResponse:
    out = ""
    response = PTTaskCompletionFunctionMessageResponse(Success=True, TaskStatus="success", Completed=True)
    responses = await SendMythicRPCResponseSearch(MythicRPCResponseSearchMessage(TaskID=completionMsg.SubtaskData.Task.ID))
    for output in responses.Responses:
        out += str(output.Response)
            
    await SendMythicRPCResponseCreate(MythicRPCResponseCreateMessage(
        TaskID=completionMsg.TaskData.Task.ID,
        Response=f"{decode_ldap(out)}"
    ))
    return response

def decode_ldap(json_string):
    task_output = ""

    # Parse the JSON string into a Python dictionary
    data = json.loads(json_string)
    
    def pad_left(s, length, char='0'):
        while len(s) < length:
            s = char + s
        return s
    
    def pad(s):
        return s if len(s) >= 2 else "0" + s
    
    for item in data:
        task_output += item["DistinguishedName"] + "\n"
        attribs = item["Attributes"]
        
        for key, attribute in attribs.items():
            value = ""

            if key == "objectguid":
                binary_string = base64.b64decode(attribute[0])
                guid_value_arr = list(binary_string)
                guid = ""
                for byte in guid_value_arr:
                    guid += pad_left(hex(byte)[2:], 2)

                guid_formatted = f"{guid[6:8]}{guid[4:6]}{guid[2:4]}{guid[0:2]}-{guid[10:12]}{guid[8:10]}-{guid[14:16]}{guid[12:14]}-{guid[16:20]}-{guid[20:]}"
                value = guid_formatted

            elif key == "objectsid":
                binary_string = base64.b64decode(attribute[0])
                buf = list(binary_string)

                version = buf[0]
                sub_authority_count = buf[1]
                identifier_authority = int("".join([hex(buf[i])[2:] for i in range(2, 8)]), 16)
                sid_string = f"S-{version}-{identifier_authority}"

                for i in range(sub_authority_count):
                    sub_auth_offset = i * 4
                    tmp = (
                        pad(hex(buf[11 + sub_auth_offset])[2:]) +
                        pad(hex(buf[10 + sub_auth_offset])[2:]) +
                        pad(hex(buf[9 + sub_auth_offset])[2:]) +
                        pad(hex(buf[8 + sub_auth_offset])[2:])
                    )
                    sid_string += f"-{int(tmp, 16)}"
                value = sid_string

            elif key in ["pwdlastset", "lastlogontimestamp", "lastlogon", "badpasswordtime", "accountexpires"]:
                binary_string = base64.b64decode(attribute[0])
                decimal_value = int.from_bytes(binary_string[:4], "little")
                date = datetime(1601, 1, 1) + timedelta(microseconds=(decimal_value / 1e4))
                value = date.isoformat()

            elif key in ["memberof", "member"]:
                value = "\n"
                for attr in attribute:
                    try:
                        value += f"\n\t\t{base64.b64decode(attr).decode('utf-8')} "
                    except UnicodeDecodeError:
                        value += f"\n\t\t(binary data) "

            elif key in ["whencreated", "whenchanged", "dscorepropagationdata"]:
                for attr in attribute:
                    try:
                        fullstamp = base64.b64decode(attr).decode('utf-8')
                        timestamp = fullstamp.split('.')[0]
                        year = int(timestamp[:4])
                        month = int(timestamp[4:6]) - 1  # months are 0-based in JavaScript
                        day = int(timestamp[6:8])
                        hour = int(timestamp[8:10])
                        minute = int(timestamp[10:12])
                        second = int(timestamp[12:14])
                        date = datetime(year, month + 1, day, hour, minute, second)
                        value += f"\n\t\t{date.isoformat()} "
                    except UnicodeDecodeError:
                        value += f"\n\t\t(binary timestamp data) "

            else:
                for attr in attribute:
                    try:
                        value += base64.b64decode(attr).decode('utf-8') + " "
                    except UnicodeDecodeError:
                        value += "(binary data) "

            task_output += f"    {key}: {value}\n"
        task_output += "\n"

    return task_output

async def default_completion_callback(completionMsg: PTTaskCompletionFunctionMessage) -> PTTaskCompletionFunctionMessageResponse:
    out = ""
    response = PTTaskCompletionFunctionMessageResponse(Success=True, TaskStatus="success", Completed=True)
    responses = await SendMythicRPCResponseSearch(MythicRPCResponseSearchMessage(TaskID=completionMsg.SubtaskData.Task.ID))
    for output in responses.Responses:
        out += str(output.Response)
            
    await SendMythicRPCResponseCreate(MythicRPCResponseCreateMessage(
        TaskID=completionMsg.TaskData.Task.ID,
        Response=f"{out}"
    ))
    return response