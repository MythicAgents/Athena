from mythic_container.PayloadBuilder import *
from mythic_container.MythicCommandBase import *
from distutils.dir_util import copy_tree
import asyncio
import os
import sys
import shutil
import tempfile
import traceback
import subprocess
import json
import pefile

def prepareWinExe(output_path):
    pe = pefile.PE(dir.path.join(output_path, "Athena.exe"))
    pe.OPTIONAL_HEADER.Subsystem = 2
    pe.write(dir.path.join(output_path, "Athena_Headless.exe"))
    pe.close()
    os.remove(dir.path.join(output_path, "Athena.exe"))
    os.rename(dir.path.join(output_path, "Athena_Headless.exe"), dir.path.join(output_path, "Athena.exe"))
    pass


def buildSlack(self, agent_build_path, c2):
    baseConfigFile = open("{}/AthenaSlack/Base.txt".format(agent_build_path.name), "r").read()
    baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
    for key, val in c2.get_parameters_dict().items():
        if key == "AESPSK":
            baseConfigFile = baseConfigFile.replace(key, val["enc_key"] if val["enc_key"] is not None else "")  
        elif key == "encrypted_exchange_check":
            if val == "T":
                baseConfigFile = baseConfigFile.replace(key, "True")
            else:
                baseConfigFile = baseConfigFile.replace(key, "False")  
        else:
            baseConfigFile = baseConfigFile.replace(str(key), str(val)) 
    with open("{}/AthenaSlack/Slack.cs".format(agent_build_path.name), "w") as f:
        f.write(baseConfigFile)
def buildDiscord(self, agent_build_path, c2):
    baseConfigFile = open("{}/AthenaDiscord/Base.txt".format(agent_build_path.name), "r").read()
    baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
    for key, val in c2.get_parameters_dict().items():
        if key == "AESPSK":
            baseConfigFile = baseConfigFile.replace(key, val["enc_key"] if val["enc_key"] is not None else "")  
        elif key == "encrypted_exchange_check":
            if val == "T":
                baseConfigFile = baseConfigFile.replace(key, "True")
            else:
                baseConfigFile = baseConfigFile.replace(key, "False")  
        else:
           baseConfigFile = baseConfigFile.replace(str(key), str(val)) 
    with open("{}/AthenaDiscord/Discord.cs".format(agent_build_path.name), "w") as f:
        f.write(baseConfigFile)
def buildSMB(self, agent_build_path, c2):
    baseConfigFile = open("{}/AthenaSMB/Base.txt".format(agent_build_path.name), "r").read()
    baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
    for key, val in c2.get_parameters_dict().items():
        if key == "AESPSK":
            baseConfigFile = baseConfigFile.replace(key, val["enc_key"] if val["enc_key"] is not None else "")  
        elif key == "encrypted_exchange_check":
            if val == "T":
                baseConfigFile = baseConfigFile.replace(key, "True")
            else:
                baseConfigFile = baseConfigFile.replace(key, "False")  
        else:
           baseConfigFile = baseConfigFile.replace(str(key), str(val)) 
    with open("{}/AthenaSMB/SMB.cs".format(agent_build_path.name), "w") as f:
        f.write(baseConfigFile)   
def buildHTTP(self, agent_build_path, c2):
    baseConfigFile = open("{}/AthenaHTTP/Base.txt".format(agent_build_path.name), "r").read()
    baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
    for key, val in c2.get_parameters_dict().items():
        if key == "AESPSK":
            baseConfigFile = baseConfigFile.replace(key, val["enc_key"] if val["enc_key"] is not None else "")
        elif key == "headers":
            customHeaders = ""
            for item in val:
                if item == "Host":
                    baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", val[item])
                elif item == "User-Agent":
                    baseConfigFile = baseConfigFile.replace("%USERAGENT%", val[item])
                else:
                    customHeaders += "this.client.DefaultRequestHeaders.Add(\"{}\", \"{}\");".format(str(item), str(val[item])) + '\n'  
            
            baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", "")
            baseConfigFile = baseConfigFile.replace("//%CUSTOMHEADERS%", customHeaders)   
        elif key == "encrypted_exchange_check":
            if val == "T":
                baseConfigFile = baseConfigFile.replace(key, "True")
            else:
                baseConfigFile = baseConfigFile.replace(key, "False")  
        else:
           baseConfigFile = baseConfigFile.replace(str(key), str(val)) 
    with open("{}/AthenaHTTP/HTTP.cs".format(agent_build_path.name), "w") as f:
        f.write(baseConfigFile)
def buildWebsocket(self, agent_build_path, c2):
    baseConfigFile = open("{}/AthenaWebsocket/Base.txt".format(agent_build_path.name), "r").read()
    baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
    for key, val in c2.get_parameters_dict().items():
        if key == "AESPSK":
            baseConfigFile = baseConfigFile.replace(key, val["enc_key"] if val["enc_key"] is not None else "")
        elif key == "headers":
            customHeaders = ""
            for item in val:
                if item == "Host":
                    baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", val[item])
                elif item == "User-Agent":
                    baseConfigFile = baseConfigFile.replace("%USERAGENT%", val[item])
                else:
                    customHeaders += "this.client.DefaultRequestHeaders.Add(\"{}\", \"{}\");".format(str(item), str(val[item])) + '\n'  
            
            baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", "")
            baseConfigFile = baseConfigFile.replace("//%CUSTOMHEADERS%", customHeaders)   
        elif key == "encrypted_exchange_check":
            if val == "T":
                baseConfigFile = baseConfigFile.replace(key, "True")
            else:
                baseConfigFile = baseConfigFile.replace(key, "False")  
        else:
           baseConfigFile = baseConfigFile.replace(str(key), str(val)) 
    with open("{}/AthenaWebsocket/Websocket.cs".format(agent_build_path.name), "w") as f:
        f.write(baseConfigFile)
def addCommand(agent_build_path, command_name, project_name):
    project_path = os.path.join(agent_build_path.name, "AthenaPlugins", command_name, "{}.csproj".format(command_name))
    p = subprocess.Popen(["dotnet", "add", project_name, "reference", project_path], cwd=agent_build_path.name)
    p.wait()
def addProfile(agent_build_path, profile):
    project_path = os.path.join(agent_build_path.name, "Athena{}".format(profile), "Athena.Profiles.{}.csproj".format(profile))
    p = subprocess.Popen(["dotnet", "add", "reference", project_path], cwd=os.path.join(agent_build_path.name, "Athena"))
    p.wait()
def addForwarder(agent_build_path, profile):
    project_path = os.path.join(agent_build_path.name, "Athena.Forwarders.{}".format(profile), "Athena.Forwarders.{}.csproj".format(profile))
    p = subprocess.Popen(["dotnet", "add", "reference", project_path], cwd=os.path.join(agent_build_path.name, "Athena"))
    p.wait()
def addHandler(agent_build_path, handler_path):
    #project_path = os.path.join(agent_build_path.name, "Athena.Forwarders.{}".format(profile), "Athena.Forwarders.{}.csproj".format(profile))
    p = subprocess.Popen(["dotnet", "add", "reference", handler_path], cwd=os.path.join(agent_build_path.name, "Athena"))
    p.wait()

# define your payload type class here, it must extend the PayloadType class though
class athena(PayloadType):
    name = "athena"  # name that would show up in the UI
    file_extension = "zip"  # default file extension to use when creating payloads
    author = "@checkymander"  # author of the payload type
    supported_os = [
        SupportedOS.Windows,
        SupportedOS.Linux,
        SupportedOS.MacOS,
        SupportedOS("RedHat"),
    ]  # supported OS and architecture combos
    wrapper = False  # does this payload type act as a wrapper for another payloads inside of it?
    wrapped_payloads = []  # if so, which payload types. If you are writing a wrapper, you will need to modify this variable (adding in your wrapper's name) in the builder.py of each payload that you want to utilize your wrapper.
    note = """A cross platform .NET compatible agent."""
    supports_dynamic_loading = True  # setting this to True allows users to only select a subset of commands when generating a payload
    agent_path = pathlib.Path(".") / "athena" / "mythic"
    agent_code_path = pathlib.Path(".") / "athena"  / "agent_code"
    agent_icon_path = agent_path / "agent_functions" / "athena.svg"
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
            name="single-file",
            parameter_type=BuildParameterType.Boolean,
            description="Publish as a single-file executable",
            default_value=True,
        ),
        BuildParameter(
            name="arch",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["x64", "x86", "arm", "arm64", "musl-x64"],
            default_value="x64",
            description="Target architecture"
        ),
        # BuildParameter(
        #     name="forwarder-type",
        #     parameter_type=BuildParameterType.ChooseOne,
        #     choices=["none", "smb"],
        #     default_value="none",
        #     description="Include the ability to forward messages over a selected channel"
        # ),
        BuildParameter(
            name="configuration",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["Release", "Debug"],
            default_value="Release",
            description="Select compiler configuration release/debug"
        ),
        # BuildParameter(
        #     name="native-aot",
        #     parameter_type=BuildParameterType.Boolean,
        #     default_value= False,
        #     description="Compile using the experimental Native AOT"
        # ),
        BuildParameter(
            name="output-type",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["exe", "source"],
            default_value="exe",
            description="Compile the payload or provide the raw source code"
        ),
    ]
    c2_profiles = ["http", "websocket", "slack", "smb", "discord"]

    async def build(self) -> BuildResponse:
        # self.Get_Parameter returns the values specified in the build_parameters above.
        resp = BuildResponse(status=BuildStatus.Error)
        build_msg = ""
        try:
            # make a Temporary Directory for the payload files
            agent_build_path = tempfile.TemporaryDirectory(suffix=self.uuid)
            # Copy files into the temp directory
            copy_tree(self.agent_code_path, agent_build_path.name)

            directives = self.get_parameter("configuration").upper()
            rid = ""
            roots_replace = ""

            #validate parameters
            # for x in self.build_parameters:
            #     if x.name == "single-file":
            #         if(self.get_parameter("native-aot") == True):
            #             x.value = False
            #     if x.name == "trimmed":
            #         if(self.get_parameter("native-aot") == True):
            #             x.value = True

            add_profile_params = ""

            for c2 in self.c2info:
                profile = c2.get_c2profile()
                build_msg += "Adding {} profile...".format(profile["name"]) + '\n'
                if profile["name"] == "http":
                    add_profile_params += "/p:HTTPProfile=True "
                    roots_replace += "<assembly fullname=\"Athena.Profiles.HTTP\"/>" + '\n'
                    buildHTTP(self, agent_build_path, c2)
                    directives += ";HTTP"
                elif profile["name"] == "smb":
                    add_profile_params += "/p:SMBProfile=True "
                    roots_replace += "<assembly fullname=\"Athena.Profiles.SMB\"/>" + '\n'
                    buildSMB(self, agent_build_path, c2)
                    directives += ";SMBPROFILE"
                elif profile["name"] == "websocket":
                    add_profile_params += "/p:WebsocketProfile=True "
                    roots_replace += "<assembly fullname=\"Athena.Profiles.Websocket\"/>" + '\n'
                    buildWebsocket(self, agent_build_path, c2)
                    directives += ";WEBSOCKET"
                elif profile["name"] == "slack":
                    add_profile_params += "/p:SlackProfile=True "
                    roots_replace += "<assembly fullname=\"Athena.Profiles.Slack\"/>" + '\n'
                    buildSlack(self, agent_build_path, c2)
                    directives += ";SLACK"
                elif profile["name"] == "discord":
                    add_profile_params += "/p:DiscordProfile=True "
                    roots_replace += "<assembly fullname=\"Athena.Profiles.Discord\"/>" + '\n'
                    buildDiscord(self, agent_build_path, c2)
                    directives += ";DISCORD"
                else:
                    raise Exception("Unsupported C2 profile type for Athena: {}".format(profile["name"]))

            stdout_err = ""
            loadable_commands = ["arp","cat","cd","coff","cp","crop","ds","drives","env","farmer","get-clipboard","get-localgroup","get-sessions","get-shares","hostname","ifconfig","inline-exec",
            "kill","ls","mkdir","mv","nslookup","patch","ps","pwd","reg","rm","sftp","shell","shellcode", "shellcode-inject","ssh","tail","test-port","timestomp","uptime","wget","whoami","win-enum-resources"]
            output_type = "Exe"
            build_msg += "Determining selected OS...{}".format(self.selected_os) + '\n'
            if self.selected_os.upper() == "WINDOWS":
                output_type = "WinExe"
                directives += ";WINBUILD"
                rid = "win-" + self.get_parameter("arch")
            elif self.selected_os.upper() == "LINUX":
                directives += ";NIXBUILD"
                rid = "linux-" + self.get_parameter("arch")
            elif self.selected_os.upper() == "MACOS":
                if self.get_parameter("arch") == "arm64":
                    rid = "osx.12-arm64"
                else:
                    rid = "osx-" + self.get_parameter("arch")
                directives += ";MACBUILD"
            elif self.selected_os.upper() == "REDHAT":
                directives += ";RHELBUILD;NIXBUILD"
                rid = "rhel-x64"
            
            build_msg += "RID set to...{}".format(rid) + '\n'
            os.environ["DOTNET_RUNTIME_IDENTIFIER"] = rid
            
            # if self.get_parameter("native-aot"):
            #     directives += ";NATIVEAOT"
            # else:
            #     directives += ";DYNAMIC"

            directives += ";DYNAMIC"

            for cmd in self.commands.get_commands():
                if cmd in loadable_commands:
                    if cmd == "ds" and self.selected_os.upper() == "REDHAT":
                        build_msg += "Ignoring ds because it's not supported on RHEL" + '\n'
                        continue
                    else:
                        try:
                            build_msg += "Adding command...{}".format(cmd) + '\n'
                            directives += ";" + cmd.replace("-","").upper()
                            roots_replace += "<assembly fullname=\"{}\"/>".format(cmd) + '\n'
                        except:
                            pass

            build_msg += "Final Directives...{}".format(directives) + '\n'

            # Replace the roots file with the new one
            baseRoots = open("{}/Athena/Roots.xml".format(agent_build_path.name), "r").read()
            baseRoots = baseRoots.replace("<!-- {{REPLACEME}} -->", roots_replace)
            with open("{}/Athena/Roots.xml".format(agent_build_path.name), "w") as f:
                f.write(baseRoots)


            if self.get_parameter("output-type") == "source":
                resp.status = BuildStatus.Success
                shutil.make_archive(f"{agent_build_path.name}/output", "zip", f"{agent_build_path.name}")
                resp.payload = open(f"{agent_build_path.name}/output.zip", 'rb').read()
                resp.message = "File built successfully!"
                resp.build_message = build_msg
                resp.build_stdout += stdout_err
                return resp

            command = "dotnet publish Athena -r {} -c {} --nologo --verbosity=q --self-contained={} /p:PublishSingleFile={} /p:EnableCompressionInSingleFile={} /p:PublishTrimmed={} /p:PublishAOT={} /p:DebugType=None /p:DebugSymbols=false /p:SolutionDir={} /p:HandlerOS={} /p:AthenaOutputType={} {}".format(
                rid, 
                self.get_parameter("configuration"), 
                self.get_parameter("self-contained"), 
                self.get_parameter("single-file"), 
                self.get_parameter("compressed"), 
                self.get_parameter("trimmed"), 
                False, #Setting native-aot to false temporarily while I explore keeping it or not.
                agent_build_path.name, 
                self.selected_os.lower(),
                output_type,
                add_profile_params)
            
            output_path = "{}/Athena/bin/{}/net7.0/{}/publish/".format(agent_build_path.name,self.get_parameter("configuration").capitalize(), rid)

            # Run the build command
            build_env = os.environ.copy()
            build_env["AthenaConstants"] = directives

            proc = await asyncio.create_subprocess_shell(command, stdout=asyncio.subprocess.PIPE,
                                                         stderr=asyncio.subprocess.PIPE,
                                                         cwd=agent_build_path.name,
                                                         env=build_env)
            stdout, stderr = await proc.communicate()

            if stdout:
                stdout_err += f'[stdout]\n{stdout.decode()}\n'
            if stderr:
                stdout_err += f'[stderr]\n{stderr.decode()}' + "\n" + command

            build_msg += "Command: " + command + '\n'
            build_msg += "Output: " + output_path + '\n'
            build_msg += "OS: " + self.selected_os + '\n'
            build_msg += "AthenConstantsVar: " + build_env["AthenaConstants"] + "\n"

            for c2 in self.c2info:
                profile = c2.get_c2profile()
                profile_name = profile["name"]
                build_msg += "Adding {} profile...".format(profile["name"]) + '\n'
                if profile["name"] == "http":
                    with open (f"{output_path}/{profile_name}.json", "w") as f:
                        #f.write(json.dumps(self.c2_profiles))
                        profile = c2.get_c2profile()
                        f.write(json.dumps(c2.get_parameters_dict()))
                elif profile["name"] == "websocket":
                    with open (f"{output_path}/{profile_name}.json", "w") as f:
                        f.write(json.dumps(c2.get_parameters_dict()))
                        profile = c2.get_c2profile()
                elif profile["name"] == "smb":
                    with open (f"{output_path}/{profile_name}.json", "w") as f:
                        #f.write(json.dumps(self.c2_profiles))
                        f.write(json.dumps(c2.get_parameters_dict()))
                        profile = c2.get_c2profile()
                elif profile["name"] == "slack":
                    with open (f"{output_path}/{profile_name}.json", "w") as f:
                        #f.write(json.dumps(self.c2_profiles))
                        f.write(json.dumps(c2.get_parameters_dict()))
                        profile = c2.get_c2profile()
                elif profile["name"] == "discord":
                    with open (f"{output_path}/{profile_name}.json", "w") as f:
                        f.write(json.dumps(c2.get_parameters_dict()))
                        #f.write(json.dumps(self.c2_profiles))
                        profile = c2.get_c2profile()

            if os.path.exists(output_path):
                if self.selected_os.upper() == "WINDOWS":
                    prepareWinExe(output_path) #Force it to be headless

                # Build worked, return payload
                shutil.make_archive(f"{agent_build_path.name}/output", "zip", f"{output_path}")
                build_msg += "Output Directory of zipfile: " + str(os.listdir(agent_build_path.name)) + "\n"
                resp.payload = open(f"{agent_build_path.name}/output.zip", 'rb').read()
                resp.status = BuildStatus.Success
                resp.message = "File built successfully!"
                resp.build_message = build_msg
                resp.build_stdout += stdout_err
            else:
                resp.status = BuildStatus.Error
                resp.payload = b""
                resp.build_message = build_msg
                resp.build_stderr += stdout_err
                resp.message += "File build failed."
        except:
            # An error occurred, return the error
            resp.payload = b""
            resp.status = BuildStatus.Error
            resp.build_message = "Error building payload: " + str(traceback.format_exc())+ '\n' + build_msg
            resp.message = "Error build payload: " + str(traceback.format_exc()) 

        sys.stdout.flush()
        return resp
