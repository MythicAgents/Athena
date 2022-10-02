from mythic_payloadtype_container.PayloadBuilder import *
from mythic_payloadtype_container.MythicCommandBase import *
from distutils.dir_util import copy_tree

import asyncio
import os
import sys
import shutil
import tempfile
import traceback
import subprocess

def buildSlack(self, agent_build_path, c2):
    baseConfigFile = open("{}/AthenaSlack/Base.txt".format(agent_build_path.name), "r").read()
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
    with open("{}/AthenaSlack/Slack.cs".format(agent_build_path.name), "w") as f:
        f.write(baseConfigFile)

def buildDiscord(self, agent_build_path, c2):
    baseConfigFile = open("{}/AthenaDiscord/Base.txt".format(agent_build_path.name), "r").read()
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
    with open("{}/AthenaDiscord/Discord.cs".format(agent_build_path.name), "w") as f:
        f.write(baseConfigFile)


def buildSMB(self, agent_build_path, c2):
    baseConfigFile = open("{}/AthenaSMB/Base.txt".format(agent_build_path.name), "r").read()
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
    with open("{}/AthenaDiscord/SMB.cs".format(agent_build_path.name), "w") as f:
        f.write(baseConfigFile)
    
def buildHTTP(self, agent_build_path, c2):
    baseConfigFile = open("{}/AthenaHTTP/Base.txt".format(agent_build_path.name), "r").read()
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
    with open("{}/AthenaHTTP/HTTP.cs".format(agent_build_path.name), "w") as f:
        f.write(baseConfigFile)

def buildWebsocket(self, agent_build_path, c2):
    baseConfigFile = open("{}/AthenaWebsocket/Base.txt".format(agent_build_path.name), "r").read()
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
    with open("{}/AthenaWebsocket/Websocket.cs".format(agent_build_path.name), "w") as f:
        f.write(baseConfigFile)

def addLibrary(agent_build_path, library_name):
    p = subprocess.Popen(["dotnet", "add", "package", library_name], cwd=agent_build_path.name)
    p.wait()

def addNativeAot(agent_build_path):
    p = subprocess.Popen(["dotnet", "add", "package", "Microsoft.DotNet.ILCompiler","-v","7.0.0-*"], cwd=os.path.join(agent_build_path.name,"Athena"))
    p.wait()

def addCommand(agent_build_path, command_name):
    project_path = os.path.join(agent_build_path.name, "AthenaPlugins", command_name, "{}.csproj".format(command_name))
    p = subprocess.Popen(["dotnet", "add", "reference", project_path], cwd=os.path.join(agent_build_path.name, "Athena"))
    p.wait()

def addProfile(agent_build_path, profile):
    project_path = os.path.join(agent_build_path.name, "Athena{}".format(profile), "Athena.Profiles.{}.csproj".format(profile))
    p = subprocess.Popen(["dotnet", "add", "reference", project_path], cwd=os.path.join(agent_build_path.name, "Athena"))
    p.wait()

def addForwarder(agent_build_path, profile):
    project_path = os.path.join(agent_build_path.name, "Athena.Forwarders.{}".format(profile), "Athena.Forwarders.{}.csproj".format(profile))
    p = subprocess.Popen(["dotnet", "add", "reference", project_path], cwd=os.path.join(agent_build_path.name, "Athena"))
    p.wait()



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
            name="self-contained",
            parameter_type=BuildParameterType.Boolean,
            description="Indicate whether the payload will include the full .NET framework",
            default_value=True,
        ),
        BuildParameter(
            name="trimmed",
            parameter_type=BuildParameterType.Boolean,
            description="Trim unnecessary assemblies. Note: This may cause issues with non-included reflected assemblies",
            default_value=False,
        ),
        BuildParameter(
            name="compressed",
            parameter_type=BuildParameterType.Boolean,
            default_value=True,
            description="If a single-file binary, compress the final binary"
        ),
        BuildParameter(
            name="ready-to-run",
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
            name="rid",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["win-x64", "win-x86", "win-arm", "win-arm64", "win7-x64", "win7-x86", "win81-x64", "win81-arm", "win10-x64", "win10-x86", "win10-arm", "win10-arm64",
            "linux-x64", "linux-musl-x64","linux-arm","linux-arm64","rhel-x64","rhel.6-x64","tizen","tizen.4.0.0","tizen.5.0.0",
            "osx-x64","osx.10.10-x64","osx.10.11-x64","osx.10.12-x64","osx.10.13-x64","osx.10.14-x64","osx.10.15-x64","osx.11.0-x64","osx.11.0-arm64","osx.12-x64","osx.12-arm64"],
            default_value="win-x64",
            description="Target architecture"
        ),
        BuildParameter(
            name="forwarder-type",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["none", "smb"],
            default_value="none",
            description="Include the ability to forward messages over a selected channel"
        ),
        BuildParameter(
            name="configuration",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["Release", "Debug"],
            default_value="release",
            description="Select compiler configuration release/debug"
        ),
        BuildParameter(
            name="native-aot",
            parameter_type=BuildParameterType.Boolean,
            default_value= False,
            description="Compile using Native AOT"
        ),
        BuildParameter(
            name="output-type",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["exe", "source"],
            default_value="exe",
            description="Compile the payload or provide the raw source code"
        ),
    ]
    #  the names of the c2 profiles that your agent supports
    c2_profiles = ["http", "websocket", "slack", "smb", "discord"]

    async def build(self) -> BuildResponse:
        # self.Get_Parameter returns the values specified in the build_parameters above.
        resp = BuildResponse(status=BuildStatus.Error)

        try:
            # make a Temporary Directory for the payload files
            agent_build_path = tempfile.TemporaryDirectory(suffix=self.uuid)
            # Copy files into the temp directory
            copy_tree(self.agent_code_path, agent_build_path.name)

            directives = self.get_parameter("configuration").upper()

            for cmd in self.commands.get_commands():
                try:
                    addCommand(agent_build_path, cmd)
                except:
                    pass

            for c2 in self.c2info:
                profile = c2.get_c2profile()
                if profile["name"] == "http":
                    buildHTTP(self, agent_build_path, c2)
                    addProfile(agent_build_path, "HTTP")
                    directives += ";HTTP"
                elif profile["name"] == "smb":
                    buildSMB(self, agent_build_path, c2)
                    addProfile(agent_build_path, "SMB")
                    directives += ";SMB"
                elif profile["name"] == "websocket":
                    buildWebsocket(self, agent_build_path, c2)
                    addProfile(agent_build_path, "Websocket")
                    directives += ";WEBSOCKET"
                elif profile["name"] == "slack":
                    buildSlack(self, agent_build_path, c2)
                    addProfile(agent_build_path, "Slack")
                    directives += ";SLACK"
                elif profile["name"] == "discord":
                    buildDiscord(self, agent_build_path, c2)
                    addProfile(agent_build_path, "Discord")
                    directives += ";DISCORD"
                else:
                    raise Exception("Unsupported C2 profile type for Athena: {}".format(profile["name"]))

            if self.get_parameter("forwarder-type") == "smb":  # SMB Forwarding selected by the user
                directives += ";SMBFWD"
                addForwarder(agent_build_path, "SMB")
            else:  # None selected
                directives += ";EMPTY"
                addForwarder(agent_build_path, "Empty")

            stdout_err = ""

            if self.get_parameter("native-aot"):
                addNativeAot(agent_build_path)

            os.environ["DOTNET_RUNTIME_IDENTIFIER"] = self.get_parameter("rid")

            if self.selected_os == "Windows":
                directives += ";WINBUILD"
                baseCSProj = open("{}/Athena/Athena.csproj".format(agent_build_path.name), "r").read()
                baseCSProj = baseCSProj.replace("TRACE", directives)
                with open("{}/Athena/Athena.csproj".format(agent_build_path.name), "w") as f:
                    f.write(baseCSProj)

                baseCSProj = open("{}/Athena.Utilities/Athena.Utilities.csproj".format(agent_build_path.name), "r").read()
                baseCSProj = baseCSProj.replace("TRACE", "TRACE;WINBUILD")
                with open("{}/Athena.Utilities/Athena.Utilities.csproj".format(agent_build_path.name), "w") as f:
                    f.write(baseCSProj)

            if self.get_parameter("output-type") == "source":
                resp.status = BuildStatus.Success
                shutil.make_archive(f"{agent_build_path.name}/", "zip", f"{agent_build_path.name}")
                resp.payload = open(agent_build_path.name.rstrip("/") + ".zip", 'rb').read()
                resp.message = "File built successfully!"
                resp.build_message = "File built successfully!"
                resp.build_stdout += stdout_err
                return resp

            command = "dotnet publish Athena -r {} -c {} --self-contained {} /p:PublishSingleFile={} /p:EnableCompressionInSingleFile={} /p:PublishReadyToRun={} /p:PublishTrimmed={}".format(self.get_parameter("rid"),self.get_parameter("configuration"), self.get_parameter("self-contained"), self.get_parameter("single-file"), self.get_parameter("compressed"),self.get_parameter("ready-to-run"), self.get_parameter("trimmed"))
            
            output_path = "{}/Athena/bin/{}/net6.0/{}/publish/".format(agent_build_path.name,self.get_parameter("configuration").capitalize(), self.get_parameter("rid"))

            # Run the build command
            proc = await asyncio.create_subprocess_shell(command, stdout=asyncio.subprocess.PIPE,
                                                         stderr=asyncio.subprocess.PIPE,
                                                         cwd=agent_build_path.name)
            stdout, stderr = await proc.communicate()

            if stdout:
                stdout_err += f'[stdout]\n{stdout.decode()}\n'
            if stderr:
                stdout_err += f'[stderr]\n{stderr.decode()}' + "\n" + command
            # Check to see if the build worked

            resp.build_stdout = "Command: " + command + '\n'
            resp.build_stdout += "Output: " + output_path + '\n'
            resp.build_stdout += "OS: " + self.selected_os + '\n'
            resp.message = "Command: " + command + '\n'
            resp.message += "Output: " + output_path + '\n'
            resp.message += "OS: " + self.selected_os + '\n'

            if os.path.exists(output_path):
                # Build worked, return payload
                resp.status = BuildStatus.Success
                shutil.make_archive(f"{output_path}/", "zip", f"{output_path}")
                resp.payload = open(output_path.rstrip("/") + ".zip", 'rb').read()
                resp.message = "File built successfully!"
                resp.build_message = "File built successfully!"
                resp.build_stdout += stdout_err
            else:
                # Build Failed, return error message
                resp.status = BuildStatus.Error
                resp.payload = b""
                resp.build_message = stdout_err
                resp.build_stderr += stdout_err
                resp.message += stdout_err
        except:
            # An error occurred, return the error
            resp.payload = b""
            resp.status = BuildStatus.Error
            resp.build_message = "Error building payload: " + str(traceback.format_exc())
            resp.message = "Error build payload: " + str(traceback.format_exc())

        sys.stdout.flush()
        return resp
