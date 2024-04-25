from mythic_container.PayloadBuilder import *
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from distutils.dir_util import copy_tree
from .athena_utils import plugin_utilities
from .athena_utils import mac_bundler
import asyncio
import os
import sys
import shutil
import tempfile
import traceback
import subprocess
import json
import pefile


# define your payload type class here, it must extend the PayloadType class though
class athena(PayloadType):
    name = "athena"  # name that would show up in the UI
    file_extension = "zip"  # default file extension to use when creating payloads
    author = "@checkymander"  # author of the payload type
    supported_os = [
        SupportedOS.Windows,
        SupportedOS.Linux,
        SupportedOS.MacOS,
    ]  # supported OS and architecture combos
    wrapper = False  # does this payload type act as a wrapper for another payloads inside of it?
    wrapped_payloads = []  # if so, which payload types. If you are writing a wrapper, you will need to modify this variable (adding in your wrapper's name) in the builder.py of each payload that you want to utilize your wrapper.
    note = """A cross platform .NET compatible agent."""
    supports_dynamic_loading = True  # setting this to True allows users to only select a subset of commands when generating a payload
    agent_path = pathlib.Path(".") / "athena" / "mythic"
    agent_code_path = pathlib.Path(".") / "athena"  / "agent_code"
    agent_icon_path = agent_path / "agent_functions" / "athena.svg"
    build_steps = [
        BuildStep(step_name="Gather Files", step_description="Copying files to temp location"),
        BuildStep(step_name="Configure C2 Profiles", step_description="Configuring C2 Profiles"),
        BuildStep(step_name="Configure Agent", step_description="Updating the Agent Configuration"),
        BuildStep(step_name="Add Tasks", step_description="Adding built-in commands to the agent"),
        BuildStep(step_name="Compile", step_description="Compiling final executable"),
        BuildStep(step_name="Zip", step_description="Zipping final payload"),
    ]
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
        BuildParameter(
            name="configuration",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["Release", "Debug"],
            default_value="Release",
            description="Select compiler configuration release/debug"
        ),
        BuildParameter(
            name="obfuscate",
            parameter_type=BuildParameterType.Boolean,
            default_value=False,
            description="Obfuscate the final payload with Obfuscar"
        ),
        BuildParameter(
            name="invariantglobalization",
            parameter_type=BuildParameterType.Boolean,
            default_value= False,
            description="Use Invariant Globalization (May cause issues with non-english systems)"
        ),
        BuildParameter(
            name="usesystemresourcekeys",
            parameter_type=BuildParameterType.Boolean,
            default_value= False,
            description="Strip Exception Messages"
        ),
        BuildParameter(
            name="stacktracesupport",
            parameter_type=BuildParameterType.Boolean,
            default_value= True,
            description="Enable Stack Trace message"
        ),
        # BuildParameter(
        #     name="optimizeforsize",
        #     parameter_type=BuildParameterType.Boolean,
        #     default_value= False,
        #     description="Compile using the experimental Native AOT"
        # ),
        # BuildParameter(
        #     name="native-aot",
        #     parameter_type=BuildParameterType.Boolean,
        #     default_value= False,
        #     description="Compile using the experimental Native AOT"
        # ),
        BuildParameter(
            name="output-type",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["binary", "source", "app bundle"],
            default_value="binary",
            description="Compile the payload or provide the raw source code"
        ),
        # BuildParameter(
        #     name="hide-window",
        #     parameter_type=BuildParameterType.Boolean,
        #     default_value=True,
        #     description="Hide the window when running the payload"
        # ),
    ]
    c2_profiles = ["http", "websocket", "slack", "smb", "discord"]

    async def prepareWinExe(self, output_path):
        pe = pefile.PE(os.path.join(output_path, "Agent.exe"))
        pe.OPTIONAL_HEADER.Subsystem = 2
        pe.write(os.path.join(output_path, "Agent_Headless.exe"))
        pe.close()
        os.remove(os.path.join(output_path, "Agent.exe"))
        os.rename(os.path.join(output_path, "Agent_Headless.exe"), os.path.join(output_path, "Athena.exe"))

    async def buildSlack(self, agent_build_path, c2):
        baseConfigFile = open("{}/Agent.Profiles.Slack/Base.txt".format(agent_build_path.name), "r").read()
        baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
        for key, val in c2.get_parameters_dict().items():
            if key == "encrypted_exchange_check":
                if val == "T":
                    baseConfigFile = baseConfigFile.replace(key, "True")
                else:
                    baseConfigFile = baseConfigFile.replace(key, "False")  
            else:
                baseConfigFile = baseConfigFile.replace(str(key), str(val)) 
        with open("{}/Agent.Profiles.Slack/SlackProfile.cs".format(agent_build_path.name), "w") as f:
            f.write(baseConfigFile)
        self.addProfile(agent_build_path, "Slack")

    async def buildDiscord(self, agent_build_path, c2):
        baseConfigFile = open("{}/Agent.Profiles.Discord/Base.txt".format(agent_build_path.name), "r").read()
        baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
        for key, val in c2.get_parameters_dict().items():
            if key == "encrypted_exchange_check":
                if val == "T":
                    baseConfigFile = baseConfigFile.replace(key, "True")
                else:
                    baseConfigFile = baseConfigFile.replace(key, "False")  
            else:
                baseConfigFile = baseConfigFile.replace(str(key), str(val)) 
        with open("{}/Agent.Profiles.Discord/DiscordProfile.cs".format(agent_build_path.name), "w") as f:
            f.write(baseConfigFile)
        self.addProfile(agent_build_path, "Discord")

    async def buildSMB(self, agent_build_path, c2):
        baseConfigFile = open("{}/Agent.Profiles.Smb/Base.txt".format(agent_build_path.name), "r").read()
        baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
        for key, val in c2.get_parameters_dict().items():
            if key == "encrypted_exchange_check":
                if val == "T":
                    baseConfigFile = baseConfigFile.replace(key, "True")
                else:
                    baseConfigFile = baseConfigFile.replace(key, "False")  
            else:
                baseConfigFile = baseConfigFile.replace(str(key), str(val)) 
        with open("{}/Agent.Profiles.Smb/SmbProfile.cs".format(agent_build_path.name), "w") as f:
            f.write(baseConfigFile)   
        self.addProfile(agent_build_path, "Smb")

    async def buildHTTP(self, agent_build_path, c2):
        baseConfigFile = open("{}/Agent.Profiles.Http/Base.txt".format(agent_build_path.name), "r").read()
        baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
        for key, val in c2.get_parameters_dict().items():
            if key == "headers":
                customHeaders = ""
                for item in val:
                    if item == "Host":
                        baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", val[item])
                    elif item == "User-Agent":
                        baseConfigFile = baseConfigFile.replace("%USERAGENT%", val[item])
                    else:
                        customHeaders += "this._client.DefaultRequestHeaders.Add(\"{}\", \"{}\");".format(str(item), str(val[item])) + '\n'  
                
                baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", "")
                baseConfigFile = baseConfigFile.replace("//%CUSTOMHEADERS%", customHeaders)   
            elif key == "encrypted_exchange_check":
                if val == "T":
                    baseConfigFile = baseConfigFile.replace(key, "True")
                else:
                    baseConfigFile = baseConfigFile.replace(key, "False")  
            else:
                baseConfigFile = baseConfigFile.replace(str(key), str(val)) 
        with open("{}/Agent.Profiles.Http/HttpProfile.cs".format(agent_build_path.name), "w") as f:
            f.write(baseConfigFile)
        self.addProfile(agent_build_path, "Http")

    async def buildWebsocket(self, agent_build_path, c2):
        baseConfigFile = open("{}/Agent.Profiles.Websocket/Base.txt".format(agent_build_path.name), "r").read()
        baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
        for key, val in c2.get_parameters_dict().items():
            if key == "domain_front":
                baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", val)
            elif key == "encrypted_exchange_check":
                if val == "T":
                    baseConfigFile = baseConfigFile.replace(key, "True")
                else:
                    baseConfigFile = baseConfigFile.replace(key, "False")  
            else:
                baseConfigFile = baseConfigFile.replace(str(key), str(val)) 
        
        # Failsafe to replace custom headers if they still don't exist
        baseConfigFile = baseConfigFile.replace("%HOSTHEADER%", "")
        #baseConfigFile = baseConfigFile.replace("//%CUSTOMHEADERS%", customHeaders) 

        with open("{}/Agent.Profiles.Websocket/WebsocketProfile.cs".format(agent_build_path.name), "w") as f:
            f.write(baseConfigFile)
        self.addProfile(agent_build_path, "Websocket")

    def buildConfig(self, agent_build_path, c2):
        #I could modify this to be more efficient, but it doesn't take that long so screw it. Maybe later.
        baseConfigFile = open("{}/Agent/Config/AgentConfig.cs".format(agent_build_path.name), "r").read()
        baseConfigFile = baseConfigFile.replace("%UUID%", self.uuid)
        for key, val in c2.get_parameters_dict().items():
            if key == "AESPSK":
                baseConfigFile = baseConfigFile.replace("%PSK%", val["enc_key"] if val["enc_key"] is not None else "")
                if val["enc_key"] is not None:
                    self.addCrypto(agent_build_path, "Aes")
                else:
                    self.addCrypto(agent_build_path, "None")
            else:
                baseConfigFile = baseConfigFile.replace(str(key), str(val))
                    
        with open("{}/Agent/Config/AgentConfig.cs".format(agent_build_path.name), "w") as f:
            f.write(baseConfigFile)

    # These could be combined but that's a later problem.
    def addCommand(self, agent_build_path, command_name):
        project_path = os.path.join(agent_build_path.name, command_name, "{}.csproj".format(command_name))
        p = subprocess.Popen(["dotnet", "add", "Agent", "reference", project_path], cwd=agent_build_path.name)
        p.wait()

    def addProfile(self, agent_build_path, profile):
        project_path = os.path.join(agent_build_path.name, "Agent.Profiles.{}".format(profile), "Agent.Profiles.{}.csproj".format(profile))
        p = subprocess.Popen(["dotnet", "add", "Agent", "reference", project_path], cwd=agent_build_path.name)
        p.wait()

    def addCrypto(self, agent_build_path, type):
        project_path = os.path.join(agent_build_path.name, "Agent.Crypto.{}".format(type), "Agent.Crypto.{}.csproj".format(type))
        p = subprocess.Popen(["dotnet", "add", "Agent", "reference", project_path], cwd=agent_build_path.name)
        p.wait()

    def addNuget(self, agent_build_path, package_name, project):
        project_path = os.path.join(agent_build_path.name, project, "{}.csproj".format(project))
        p = subprocess.Popen(["dotnet", "add", project_path, "package",  package_name], cwd=agent_build_path.name)
        p.wait()

    # def bundleApp(self, agent_build_path, rid, configuration):
    #     p = subprocess.Popen(["dotnet", "msbuild", "-t:BundleApp", "-p:RuntimeIdentifier={}".format(rid), "-p:Configuration={}".format(configuration), "-p:TargetFramework=net7.0"], cwd=os.path.join(agent_build_path.name, "Agent"))
    #     p.wait()
        
    #def bundleApp(self, output_path):


    async def returnSuccess(self, resp: BuildResponse, build_msg, agent_build_path) -> BuildResponse:
        resp.status = BuildStatus.Success
        resp.build_message = build_msg
        resp.payload = open(f"{agent_build_path.name}/output.zip", 'rb').read()
        return resp     
    
    async def returnFailure(self, resp: BuildResponse, err_msg, build_msg) -> BuildResponse:
        resp.status = BuildStatus.Error
        resp.payload = b""
        resp.build_message = build_msg
        resp.build_stderr = err_msg
        return resp
    
    def getRid(self):
        if self.selected_os.upper() == "WINDOWS":
            return "win-" + self.get_parameter("arch")
        elif self.selected_os.upper() == "LINUX":
            return "linux-" + self.get_parameter("arch")
        elif self.selected_os.upper() == "MACOS":
                return "osx-" + self.get_parameter("arch")
        elif self.selected_os.upper() == "REDHAT":
            return "rhel-x64"
        
    async def updateRootsFile(self, agent_build_path, roots_replace):
            baseRoots = open("{}/Agent/Roots.xml".format(agent_build_path.name), "r").read()
            baseRoots = baseRoots.replace("<!-- {{REPLACEME}} -->", roots_replace)
            with open("{}/Agent/Roots.xml".format(agent_build_path.name), "w") as f:
                f.write(baseRoots)   



    async def getBuildCommand(self, rid):
             return "dotnet publish Agent -r {} -c {} --nologo --self-contained={} /p:PublishSingleFile={} /p:EnableCompressionInSingleFile={} \
                /p:PublishTrimmed={} /p:Obfuscate={} /p:PublishAOT={} /p:DebugType=None /p:DebugSymbols=false /p:PluginsOnly=false \
                /p:HandlerOS={} /p:UseSystemResourceKeys={} /p:InvariantGlobalization={} /p:StackTraceSupport={}".format(
                rid, 
                self.get_parameter("configuration"), 
                self.get_parameter("self-contained"), 
                self.get_parameter("single-file"), 
                self.get_parameter("compressed"), 
                self.get_parameter("trimmed"), 
                self.get_parameter("obfuscate"),
                False, #Setting native-aot to false temporarily while I explore keeping it or not.
                self.selected_os.lower(),
                self.get_parameter("usesystemresourcekeys"),
                self.get_parameter("invariantglobalization"),
                self.get_parameter("stacktracesupport"))
        
    async def build(self) -> BuildResponse:
        # self.Get_Parameter returns the values specified in the build_parameters above.
        resp = BuildResponse(status=BuildStatus.Error)
        try:
            # make a Temporary Directory for the payload files
            agent_build_path = tempfile.TemporaryDirectory(suffix=self.uuid)

            if self.get_parameter("output-type") == "app bundle":
                if self.selected_os.upper() != "MACOS":
                    return await self.returnFailure(resp, "Error building payload: App Bundles are only supported on MacOS", "Error occurred while building payload. Check stderr for more information.")
                #self.addNuget(agent_build_path, "Dotnet.Bundle", "Agent")



            # Copy files into the temp directory
            copy_tree(self.agent_code_path, agent_build_path.name)
            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                PayloadUUID=self.uuid,
                StepName="Gather Files",
                StepStdout="Successfully created temporary directory at {}".format(agent_build_path.name),
                StepSuccess=True
            ))     

            rid = ""
            roots_replace = ""

            for c2 in self.c2info:
                profile = c2.get_c2profile()
                if profile["name"] == "http":
                    roots_replace += "<assembly fullname=\"Agent.Profiles.HTTP\"/>" + '\n'
                    await self.buildHTTP(agent_build_path, c2)
                elif profile["name"] == "smb":
                    roots_replace += "<assembly fullname=\"Agent.Profiles.SMB\"/>" + '\n'
                    await self.buildSMB(agent_build_path, c2)
                elif profile["name"] == "websocket":
                    roots_replace += "<assembly fullname=\"Agent.Profiles.Websocket\"/>" + '\n'
                    await self.buildWebsocket(agent_build_path, c2)
                elif profile["name"] == "slack":
                    roots_replace += "<assembly fullname=\"Agent.Profiles.Slack\"/>" + '\n'
                    await self.buildSlack(agent_build_path, c2)
                elif profile["name"] == "discord":
                    roots_replace += "<assembly fullname=\"Agent.Profiles.Discord\"/>" + '\n'
                    await self.buildDiscord(agent_build_path, c2)
                else:
                    raise Exception("Unsupported C2 profile type for Athena: {}".format(profile["name"]))
            
            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                PayloadUUID=self.uuid,
                StepName="Configure C2 Profiles",
                StepStdout="Successfully configured c2 profiles and added to agent",
                StepSuccess=True
            ))

            self.buildConfig(agent_build_path, c2)

            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                PayloadUUID=self.uuid,
                StepName="Configure Agent",
                StepStdout="Successfully replaced agent configuration",
                StepSuccess=True
            ))

            unloadable_commands = plugin_utilities.get_unloadable_commands()


            rid = self.getRid()   

            for cmd in self.commands.get_commands():
                if cmd in unloadable_commands:
                    continue

                if cmd == "nidhogg":
                    for nidhoggCommand in plugin_utilities.get_nidhogg_commands():
                        self.commands.add_command(nidhoggCommand)
                        
                if cmd == "ds":
                    if self.selected_os.lower() == "redhat":
                        continue
                    
                    for dsCommand in plugin_utilities.get_ds_commands():
                        self.commands.add_command(dsCommand)

                if cmd == "coff":
                    for coffCommand in plugin_utilities.get_coff_commands():
                        self.commands.add_command(coffCommand)
                
                if cmd == "inject-shellcode":
                    for shellcodeCommand in plugin_utilities.get_inject_shellcode_commands():
                        self.commands.add_command(shellcodeCommand)

                try:
                    self.addCommand(agent_build_path, cmd)
                    roots_replace += "<assembly fullname=\"{}\"/>".format(cmd) + '\n'
                except:
                    pass

            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                PayloadUUID=self.uuid,
                StepName="Add Tasks",
                StepStdout="Successfully added tasks to agent",
                StepSuccess=True
            ))   

            await self.updateRootsFile(agent_build_path, roots_replace)

            if self.get_parameter("output-type") == "source":
                shutil.make_archive(f"{agent_build_path.name}/output", "zip", f"{agent_build_path.name}")
                return await self.returnSuccess(resp, "File built succesfully!", agent_build_path)


            command = await self.getBuildCommand(rid)

            if(self.get_parameter("trimmed") == True):
                command += " /p:OptimizationPreference=Size"
            
            output_path = "{}/Agent/bin/{}/net7.0/{}/publish/".format(agent_build_path.name,self.get_parameter("configuration").capitalize(), rid)


            #Run command and get output
            proc = await asyncio.create_subprocess_shell(command, stdout=asyncio.subprocess.PIPE,
                                                         stderr=asyncio.subprocess.PIPE,
                                                         cwd=agent_build_path.name)
            output, err = await proc.communicate()
            print("stdout: " + str(output))
            print("stderr: " + str(err))
            sys.stdout.flush()

            if proc.returncode != 0:
                await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                    PayloadUUID=self.uuid,
                    StepName="Compile",
                    StepStdout="Error occurred while building payload. Check stderr for more information.",
                    StepSuccess=False
                ))

                return await self.returnFailure(resp, "Error building payload: " + str(err) + '\n' + str(output) + '\n' + command, "Error occurred while building payload. Check stderr for more information.")


            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                    PayloadUUID=self.uuid,
                    StepName="Compile",
                    StepStdout="Successfully compiled payload",
                    StepSuccess=True
                ))

            #If we get here, the path should exist since the build succeeded
            if self.selected_os.lower() == "windows" and self.get_parameter("configuration") != "Debug":
                await self.prepareWinExe(output_path) #Force it to be headless

            if self.get_parameter("output-type") == "app bundle":
                mac_bundler.create_app_bundle("Agent", os.path.join(output_path, "Agent"), output_path)
                os.remove(os.path.join(output_path, "Agent"))

            shutil.make_archive(f"{agent_build_path.name}/output", "zip", f"{output_path}")  

            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                    PayloadUUID=self.uuid,
                    StepName="Zip",
                    StepStdout="Successfully zipped payload",
                    StepSuccess=True
                ))   
            
            return await self.returnSuccess(resp, "File built succesfully!", agent_build_path)
        except:
            return await self.returnFailure(resp, str(traceback.format_exc()), "Exception in builder.py")
    
    
