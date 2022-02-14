from mythic_payloadtype_container.PayloadBuilder import *
from mythic_payloadtype_container.MythicCommandBase import *
from distutils.dir_util import copy_tree

import asyncio
import os
import sys
import shutil
import tempfile
import traceback


# define your payload type class here, it must extend the PayloadType class though
class athena(PayloadType):
    name = "athena"  # name that would show up in the UI
    file_extension = "zip"  # default file extension to use when creating payloads
    author = "@checkymander"  # author of the payload type
    supported_os = [
        SupportedOS.Windows,
        SupportedOS.Linux,
        SupportedOS.MacOS
    ]  # supported OS and architecture combos
    wrapper = False  # does this payload type act as a wrapper for another payloads inside of it?
    wrapped_payloads = []  # if so, which payload types. If you are writing a wrapper, you will need to modify this variable (adding in your wrapper's name) in the builder.py of each payload that you want to utilize your wrapper.
    note = """A cross platform .NET 6.0 compatible agent."""
    supports_dynamic_loading = True  # setting this to True allows users to only select a subset of commands when generating a payload
    build_parameters = [
        #  these are all the build parameters that will be presented to the user when creating your payload
        BuildParameter(
            name="version",
            parameter_type=BuildParameterType.ChooseOne,
            description="Choose a target .NET Framework",
            choices=["6.0"],
        ),
        BuildParameter(
            name="self-contained",
            parameter_type=BuildParameterType.Boolean,
            description="Indicate whether the payload will include the full .NET framework",
            default_value=True,
        ),
        BuildParameter(
            name="trimmed",
            parameter_type=BuildParameterType.Boolean,
            description="Trim unnecessary assemblies. Note: This will decrease the file size, while disabling reflection capabilities",
            default_value=False,
        ),
        BuildParameter(
            name="compressed",
            parameter_type=BuildParameterType.Boolean,
            default_value=True,
            description="If a single-file binary, compress the final binary"
        ),
        BuildParameter(
            name="aot-compilation",
            parameter_type=BuildParameterType.Boolean,
            default_value=False,
            description="Enable ahead-of-time (AOT) compilation. https://docs.microsoft.com/en-us/dotnet/core/deploying/ready-to-run"
        ),
        BuildParameter(
            name="single-file",
            parameter_type=BuildParameterType.Boolean,
            description="Publish as a single-file executable",
            default_value=True,
        ),
        BuildParameter(
            name="arch",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["x64", "x86", "amd64", "AnyCPU"],
            default_value="x64",
            description="Target architecture"
        ),
        BuildParameter(
            name="smb_forwarding",
            parameter_type=BuildParameterType.Boolean,
            default_value=True,
            description="Include the ability to forward messages over SMB"
        ),
        # "obfuscate": BuildParameter(
        #    name="obfuscate",
        #    parameter_type=BuildParameterType.ChooseOne,
        #    description="Obfuscate the payload using ConfuserEx. Default: False",
        #    default_value=False,
        # ),
        BuildParameter(
            name="default_proxy",
            parameter_type=BuildParameterType.Boolean,
            default_value=False, 
            required=False,
            description="Use the default proxy on the system, either true or false"),
    ]
    #  the names of the c2 profiles that your agent supports
    c2_profiles = ["http", "websocket", "smb"]

    async def build(self) -> BuildResponse:
        # self.Get_Parameter returns the values specified in the build_parameters above.
        resp = BuildResponse(status=BuildStatus.Error)

        try:
            # make a Temporary Directory for the payload files
            agent_build_path = tempfile.TemporaryDirectory(suffix=self.uuid)
            # Copy files into the temp directory
            copy_tree(self.agent_code_path, agent_build_path.name)

            # Rewrite the config.cs with the proper values assigned above.
            for c2 in self.c2info:
                profile = c2.get_c2profile()
                if profile["name"] == "http":
                    baseConfigFile = open("{}/Athena/Config/Templates/HTTP.txt".format(agent_build_path.name), "r").read()
                    baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
                    for key, val in c2.get_parameters_dict().items():
                        if isinstance(val, dict):
                            baseConfigFile = baseConfigFile.replace(key, val["enc_key"] if val["enc_key"] is not None else "")
                        elif key == "headers":
                            hl = val
                            hl = {n["key"]: n["value"] for n in hl}
                            # baseConfigFile = baseConfigFile.replace("%USERAGENT%", hl["User-Agent"])
                            if "Host" in hl:
                                baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", hl["Host"])
                            else:
                                baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", "")
                                
                            if "User-Agent" in hl:
                                baseConfigFile = baseConfigFile.replace("%USERAGENT%", hl["User-Agent"])
                            else:
                                baseConfigFile = baseConfigFile.replace("%USERAGENT%", "")
                                
                        elif key == "encrypted_exchange_check":
                            if val == "T":
                                baseConfigFile = baseConfigFile.replace(key, "True")
                            else:
                                baseConfigFile = baseConfigFile.replace(key, "False")
                        else:
                            baseConfigFile = baseConfigFile.replace(key, val)
                    with open("{}/Athena/Config/MythicConfig.cs".format(agent_build_path.name), "w") as f:
                        f.write(baseConfigFile)
                elif profile["name"] == "smb":
                    baseConfigFile = open("{}/Athena/Config/Templates/SMB.txt".format(agent_build_path.name), "r").read()
                    baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
                    for key, val in c2.get_parameters_dict().items():
                        if isinstance(val, dict):
                            baseConfigFile = baseConfigFile.replace(key, val["enc_key"] if val["enc_key"] is not None else "")
                        elif key == "encrypted_exchange_check":
                            if val == "T":
                                baseConfigFile = baseConfigFile.replace(key, "True")
                            else:
                                baseConfigFile = baseConfigFile.replace(key, "False")
                        else:
                            baseConfigFile = baseConfigFile.replace(key, val)
                    with open("{}/Athena/Config/MythicConfig.cs".format(agent_build_path.name), "w") as f:
                        f.write(baseConfigFile)
                elif profile["name"] == "websocket":
                    baseConfigFile = open("{}/Athena/Config/Templates/Websocket.txt".format(agent_build_path.name), "r").read()
                    baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
                    for key, val in c2.get_parameters_dict().items():
                        if isinstance(val, dict):
                            baseConfigFile = baseConfigFile.replace(key, val["enc_key"] if val["enc_key"] is not None else "")
                        elif key == "headers":
                            hl = val
                            hl = {n["key"]: n["value"] for n in hl}
                            if "Host" in hl:
                                baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", hl["Host"])
                            else:
                                baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", "")
                                
                            if "User-Agent" in hl:
                                baseConfigFile = baseConfigFile.replace("%USERAGENT%", hl["User-Agent"])
                            else:
                                baseConfigFile = baseConfigFile.replace("%USERAGENT%", "")
                                
                        elif key == "encrypted_exchange_check":
                            if val == "T":
                                baseConfigFile = baseConfigFile.replace(key, "True")
                            else:
                                baseConfigFile = baseConfigFile.replace(key, "False")
                        else:
                            baseConfigFile = baseConfigFile.replace(key, val)
                    with open("{}/Athena/Config/MythicConfig.cs".format(agent_build_path.name), "w") as f:
                        f.write(baseConfigFile)
                    pass
                else:
                    raise Exception("Unsupported C2 profile type for Athena: {}".format(profile["name"]))

            if self.get_parameter("smb_forwarding") == True:
                baseConfigFile = open("{}/Athena/Config/Templates/SMBForwarder.txt".format(agent_build_path.name), "r").read()
                with open("{}/Athena/Config/SMBForwarder.cs".format(agent_build_path.name), "w") as f:
                    f.write(baseConfigFile)
            else:
                baseConfigFile = open("{}/Athena/Config/Templates/SMBForwarderEmpty.txt".format(agent_build_path.name), "r").read()
                with open("{}/Athena/Config/SMBForwarder.cs".format(agent_build_path.name), "w") as f:
                    f.write(baseConfigFile)
                    
            command = "nuget restore; dotnet publish"
            output_path = agent_build_path.name + "/Athena/bin/Release/net6.0/"

            if self.selected_os == "macOS":
                if self.get_parameter("arch") == "x64":
                    output_path += "osx-x64/publish/"
                    command += " -r osx-x64"
                elif self.get_parameter("arch") == "arm64":
                    output_path += "osx.11.0-arm64/publish/"
                    command += " -r osx.11.0-arm64"
                else:
                    resp.payload = b""
                    resp.status = BuildStatus.Error
                    resp.build_message = "Architecture selected for MacOS not supported"

            elif self.selected_os == "Windows":
                if self.get_parameter("arch") == "x64":
                    output_path += "win-x64/publish/"
                    command += " -r win-x64"
                elif self.get_parameter("arch") == "x86":
                    output_path += "win-x86/publish/"
                    command += " -r win-x86"
                elif self.get_parameter("arch") == "arm64":
                    output_path += "win-arm64/publish/"
                    command += " -r win-arm64"
                elif self.get_parameter("arch") == "arm":
                    output_path += "win-arm/publish/"
                    command += " -r win-arm"
                else:
                    resp.payload = b""
                    resp.status = BuildStatus.Error
                    resp.build_message = "Architecture selected for Windows not supported"

            elif self.selected_os == "Linux":
                if self.get_parameter("arch") == "x64":
                    output_path += "linux-x64/publish/"
                    command += " -r linux-x64"
                elif self.get_parameter("arch") == "arm":
                    output_path += "linux-arm/publish/"
                    command += " -r linux-arm"
                elif self.get_parameter("arch") == "arm64":
                    output_path += "linux-arm64/publish/"
                    command += " -r linux-arm64"

            command += " -c Release"

            if self.get_parameter("self-contained") == True:
                command += " --self-contained true /p:IncludeNativeLibrariesForSelfExtract=true"

            if self.get_parameter("single-file") == True:
                command += " /p:PublishSingleFile=true"
                if self.get_parameter("compressed") == True:
                    command += " /p:EnableCompressionInSingleFile=true"

            if self.get_parameter("trimmed") == True:
                command += " /p:PublishTrimmed=true"

            # Run the build command
            proc = await asyncio.create_subprocess_shell(command, stdout=asyncio.subprocess.PIPE,
                                                         stderr=asyncio.subprocess.PIPE,
                                                         cwd=agent_build_path.name)
            stdout, stderr = await proc.communicate()
            stdout_err = ""
            if stdout:
                stdout_err += f'[stdout]\n{stdout.decode()}\n'
            if stderr:
                stdout_err += f'[stderr]\n{stderr.decode()}' + "\n" + command
            # Check to see if the build worked

            if os.path.exists(output_path):
                # Build worked, return payload
                resp.status = BuildStatus.Success
                shutil.make_archive(f"{output_path}/", "zip", f"{output_path}")
                resp.payload = open(output_path.rstrip("/") + ".zip", 'rb').read()
                resp.message = "File built successfully!"
                resp.build_message = "File built successfully!"
            else:
                # Build Failed, return error message
                resp.status = BuildStatus.Error
                resp.payload = b""
                resp.build_message = stdout_err
                resp.build_stderr = stdout_err
                resp.message = stdout_err

        except:
            # An error occurred, return the error
            resp.payload = b""
            resp.status = BuildStatus.Error
            resp.build_message = "Error building payload: " + str(traceback.format_exc())
            resp.message = "Error build payload: " + str(traceback.format_exc())

        sys.stdout.flush()
        return resp
