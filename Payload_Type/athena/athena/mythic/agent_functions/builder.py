import string
from mythic_container.PayloadBuilder import *
from mythic_container.MythicCommandBase import *
from mythic_container.MythicRPC import *
from mythic_container.logging import *

from .athena_utils import plugin_utilities
from .athena_utils import mac_bundler
from .athena_utils.assembly_utilities import effective_assembly_name
from .athena_utils.process_utilities import run_checked
from .config_generator import normalize_agent_config, write_agent_config, write_profile_config
import asyncio
import os
import sys
import shutil
import shlex
import tempfile
import traceback
import subprocess
import pefile
import random
import hashlib
import re
import pathlib
import json
import time
import stat
import xml.etree.ElementTree as ET
from xml.sax.saxutils import quoteattr
import fcntl
from contextlib import asynccontextmanager


# Safe for both a .NET simple assembly name and a cross-platform file name:
# 1-128 ASCII characters, alphanumeric at both ends, with only . _ - inside.
ASSEMBLY_NAME_PATTERN = re.compile(
    r"[A-Za-z0-9](?:[A-Za-z0-9._-]{0,126}[A-Za-z0-9])?\Z"
)
WINDOWS_RESERVED_DEVICE_PATTERN = re.compile(
    r"(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\Z", re.IGNORECASE
)
def derive_obfuscation_seed(agent_uuid):
    digest = hashlib.sha256(agent_uuid.encode()).hexdigest()
    return int(digest, 16) & 0x7FFFFFFF


# define your payload type class here, it must extend the PayloadType class though
class athena(PayloadType):
    CRYPTO_PROVIDERS = frozenset(("Aes", "None"))
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
        BuildStep(step_name="Gather Files", step_description="Copying files to temp location"),
        BuildStep(step_name="Configure C2 Profiles", step_description="Configuring C2 Profiles"),
        BuildStep(step_name="Configure Agent", step_description="Updating the Agent Configuration"),
        BuildStep(step_name="Add Tasks", step_description="Generating project references and adding built-in commands"),
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
        BuildParameter(
            name="assemblyname",
            parameter_type=BuildParameterType.String,
            default_value=''.join(random.choices(string.ascii_uppercase + string.digits, k=10)),
            description="Assembly Name"
        ),

        BuildParameter(
            name="output-type",
            parameter_type=BuildParameterType.ChooseOne,
            choices=["binary", "windows service", "source", "app bundle"],
            default_value="binary",
            description="Compile the payload or provide the raw source code"
        ),

    ]
    c2_profiles = ["http", "websocket", "smb", "discord", "github", "zoom"]

    def _validated_assembly_name(self):
        assembly_name = self.get_parameter("assemblyname")
        if not isinstance(assembly_name, str) or not ASSEMBLY_NAME_PATTERN.fullmatch(assembly_name):
            raise ValueError(
                "Invalid assemblyname: expected 1-128 ASCII characters, "
                "alphanumeric at both ends, using only letters, digits, '.', '_', or '-'"
            )
        if WINDOWS_RESERVED_DEVICE_PATTERN.fullmatch(assembly_name.split(".", 1)[0]):
            raise ValueError("Invalid assemblyname: reserved Windows device name")
        return assembly_name

    def prepareWinExe(self, output_path):
        assembly_name = self._validated_assembly_name()
        pe = pefile.PE(os.path.join(output_path, "{}.exe".format(assembly_name)))
        pe.OPTIONAL_HEADER.Subsystem = 2
        pe.write(os.path.join(output_path, "Agent_Headless.exe"))
        pe.close()
        os.remove(os.path.join(output_path,"{}.exe".format(assembly_name)))
        os.rename(os.path.join(output_path, "Agent_Headless.exe"), os.path.join(output_path, "Athena.exe"))

    PROFILE_METADATA = {
        "http": {"root": "Agent.Profiles.HTTP", "project": "Http"},
        "smb": {"root": "Agent.Profiles.SMB", "project": "Smb"},
        "websocket": {
            "root": "Agent.Profiles.Websocket",
            "project": "Websocket",
        },
        "discord": {"root": "Agent.Profiles.Discord", "project": "Discord"},
        "github": {"root": "Agent.Profiles.GitHub", "project": "GitHub"},
        "zoom": {"root": "Agent.Profiles.Zoom", "project": "Zoom"},
    }

    async def buildProfile(self, agent_build_path, c2, profile_name):
        if profile_name not in self.PROFILE_METADATA:
            raise ValueError("Unsupported C2 profile type for Athena: {}".format(profile_name))
        write_profile_config(agent_build_path.name, profile_name, c2.get_parameters_dict())
        await self.addProfile(
            agent_build_path, self.PROFILE_METADATA[profile_name]["project"]
        )

    async def buildConfig(self, agent_build_path, parameters):
        parameters = {
            **parameters,
            "obfuscate": bool(self.get_parameter("obfuscate")),
        }
        _, crypto = write_agent_config(
            agent_build_path.name, self.uuid, parameters
        )
        await self.addCrypto(agent_build_path, crypto)

    @classmethod
    def _validated_crypto_provider(cls, provider):
        if provider not in cls.CRYPTO_PROVIDERS:
            raise ValueError("Invalid crypto provider: expected Aes or None")
        return provider

    def _configured_crypto_provider(self):
        parameters = {}
        for c2 in self.c2info:
            parameters = c2.get_parameters_dict()
        return self._validated_crypto_provider(normalize_agent_config("", parameters)[1])

    _run_checked = staticmethod(run_checked)
    _cache_root = pathlib.Path(
        os.environ.get(
            "ATHENA_BUILD_CACHE_ROOT",
            os.path.join(tempfile.gettempdir(), "athena-incremental-cache-v1"),
        )
    )
    _cache_entry_limit = 12
    _cache_byte_limit = 2 * 1024 * 1024 * 1024
    _CACHE_ENTRY_PATTERN = re.compile(r"[0-9a-f]{64}\Z")
    _CACHE_STAGING_PATTERN = re.compile(r"([0-9a-f]{64})\.staging\..+\Z")
    _cache_staging_ttl_seconds = 60 * 60

    _IGNORED_SOURCE_DIRECTORIES = {
        ".vs", ".pytest_cache", "bin", "obj", "tests", "testresults",
        "__pycache__"
    }
    _TRUSTED_GENERATED_FILE = pathlib.PurePosixPath(
        "Tools/AssemblyNameObfuscator/bin/Release/net10.0/AssemblyNameObfuscator.dll"
    )

    @classmethod
    def _iter_filtered_source_files(cls, source):
        source = pathlib.Path(source)
        trusted = cls._TRUSTED_GENERATED_FILE
        for root, directories, files in os.walk(source):
            root_path = pathlib.Path(root)
            relative_root = root_path.relative_to(source)
            directories.sort()
            kept_directories = []
            for directory in directories:
                relative = pathlib.PurePosixPath(
                    (relative_root / directory).as_posix()
                )
                if (
                    directory.lower() not in cls._IGNORED_SOURCE_DIRECTORIES
                    or trusted.is_relative_to(relative)
                ):
                    kept_directories.append(directory)
            directories[:] = kept_directories
            for filename in sorted(files):
                relative = pathlib.PurePosixPath(
                    (relative_root / filename).as_posix()
                )
                lower_name = filename.lower()
                if any(
                    part.lower() in cls._IGNORED_SOURCE_DIRECTORIES
                    for part in relative.parts
                ):
                    if relative != trusted:
                        continue
                if (
                    lower_name.endswith((".dmp", ".dump", ".binlog", ".pyc"))
                    or lower_name == "output.zip"
                    or lower_name.endswith("-output.zip")
                ):
                    continue
                yield relative, root_path / filename

    @classmethod
    def _copy_filtered_source(cls, source, destination):
        destination = pathlib.Path(destination)
        for relative, source_file in cls._iter_filtered_source_files(source):
            target = destination / pathlib.Path(*relative.parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source_file, target)

    @staticmethod
    def _toolchain_identity():
        executable = shutil.which("dotnet")
        if executable is None:
            return {"dotnet": "unavailable"}
        executable_path = pathlib.Path(executable).resolve()
        try:
            executable_stat = executable_path.stat()
            sdk_root = executable_path.parent / "sdk"
            sdk_versions = (
                sorted(path.name for path in sdk_root.iterdir() if path.is_dir())
                if sdk_root.is_dir()
                else []
            )
            return {
                "dotnet_size": executable_stat.st_size,
                "dotnet_mtime_ns": executable_stat.st_mtime_ns,
                "sdk_versions": sdk_versions,
            }
        except OSError:
            return {"dotnet": "unreadable"}

    def _structural_cache_key(self):
        # Only compilation-shaping inputs belong here. Per-payload UUIDs and C2
        # values are deliberately excluded. Projects containing generated
        # per-payload configuration are never persisted in this cache.
        parameter_names = (
            "arch", "compressed", "configuration",
            "invariantglobalization", "obfuscate", "output-type",
            "self-contained", "single-file", "stacktracesupport", "trimmed",
            "usesystemresourcekeys",
        )
        structure = {
            "cache_schema": 4,
            "toolchain": self._toolchain_identity(),
            "os": self.selected_os.lower(),
            "crypto_provider": self._configured_crypto_provider(),
            "parameters": {
                name: self.get_parameter(name) for name in parameter_names
            },
            "profiles": sorted(c2.get_c2profile()["name"] for c2 in self.c2info),
            "commands": sorted(self.commands.get_commands()),
        }
        digest = hashlib.sha256(
            json.dumps(structure, sort_keys=True, separators=(",", ":")).encode()
        )
        for relative, source_file in self._iter_filtered_source_files(self.agent_code_path):
            digest.update(relative.as_posix().encode())
            digest.update(b"\0")
            with source_file.open("rb") as handle:
                for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                    digest.update(chunk)
            digest.update(b"\0")
        return digest.hexdigest()

    async def addCommands(self, agent_build_path, commands):
        self._project_references.extend(commands)

    async def addProfile(self, agent_build_path, profile):
        project_path = os.path.join(agent_build_path.name, "Agent.Profiles.{}".format(profile), "Agent.Profiles.{}.csproj".format(profile))
        self._project_references.append(os.path.relpath(project_path, agent_build_path.name))

    async def addCrypto(self, agent_build_path, type):
        provider = self._validated_crypto_provider(type)
        project_path = pathlib.Path(agent_build_path.name) / "AthenaCore" / "AthenaCore.csproj"
        source = project_path.read_text()
        pattern = re.compile(r"(<CryptoProvider(?:\s[^>]*)?>)([^<]*)(</CryptoProvider>)")
        if len(pattern.findall(source)) != 1:
            raise ValueError("AthenaCore.csproj must define exactly one CryptoProvider property")
        updated = pattern.sub(lambda match: match.group(1) + provider + match.group(3), source)
        ET.fromstring(updated)
        project_path.write_text(updated)
        self._crypto_provider = provider

    @staticmethod
    def _normalized_reference(path):
        normalized = os.path.normpath(str(path).replace("\\", "/")).replace("\\", "/")
        while normalized.startswith("../"):
            normalized = normalized[3:]
        return normalized

    def _write_project_references(self, workspace, references):
        project_path = pathlib.Path(workspace) / "AthenaCore" / "AthenaCore.csproj"
        source = project_path.read_text()
        root = ET.fromstring(source)
        existing = set()
        for item_group in root.iter():
            if (
                item_group.tag.rsplit("}", 1)[-1] != "ItemGroup"
                or "Condition" in item_group.attrib
            ):
                continue
            for element in item_group:
                if (
                    element.tag.rsplit("}", 1)[-1] == "ProjectReference"
                    and "Include" in element.attrib
                    and "Condition" not in element.attrib
                ):
                    existing.add(
                        self._normalized_reference(element.attrib["Include"])
                    )
        additions = []
        for reference in sorted(references, key=self._normalized_reference):
            normalized = self._normalized_reference(reference)
            if normalized in existing:
                continue
            existing.add(normalized)
            additions.append("../" + normalized)
        if not additions:
            return
        closing = source.rfind("</Project>")
        if closing < 0:
            raise ValueError("AthenaCore.csproj has no closing Project element")
        item_group = "  <ItemGroup>\n{}  </ItemGroup>\n".format(
            "".join(
                "    <ProjectReference Include={} />\n".format(quoteattr(reference))
                for reference in additions
            )
        )
        updated = source[:closing] + item_group + source[closing:]
        ET.fromstring(updated)
        project_path.write_text(updated)

    @staticmethod
    def _cache_token(key):
        return hashlib.sha256(str(key).encode()).hexdigest()

    def _cache_lock_path(self, token):
        # A fixed 4096-shard namespace bounds lock-file growth while allowing
        # unrelated cache keys to build concurrently in nearly all cases.
        return self._cache_root / "locks" / (token[:3] + ".lock")

    @staticmethod
    def _reject_symlink_components(path):
        path = pathlib.Path(path).absolute()
        current = pathlib.Path(path.anchor)
        for part in path.parts[1:]:
            current /= part
            try:
                metadata = current.lstat()
            except FileNotFoundError:
                continue
            if stat.S_ISLNK(metadata.st_mode):
                raise OSError("cache path contains a symbolic link")

    @classmethod
    def _ensure_private_cache_directory(cls, path):
        path = pathlib.Path(path)
        cls._reject_symlink_components(path)
        path.mkdir(parents=True, exist_ok=True, mode=0o700)
        cls._reject_symlink_components(path)
        flags = os.O_RDONLY | os.O_DIRECTORY
        if hasattr(os, "O_NOFOLLOW"):
            flags |= os.O_NOFOLLOW
        descriptor = os.open(path, flags)
        try:
            metadata = os.fstat(descriptor)
            if not stat.S_ISDIR(metadata.st_mode) or metadata.st_uid != os.geteuid():
                raise OSError("cache directory is not owned by the current user")
            os.fchmod(descriptor, 0o700)
        finally:
            os.close(descriptor)

    def _ensure_private_cache_root(self):
        self._ensure_private_cache_directory(self._cache_root)
        self._ensure_private_cache_directory(self._cache_root / "locks")
        self._ensure_private_cache_directory(self._cache_root / "artifacts")

    @asynccontextmanager
    async def _incremental_cache_guard(self, key):
        self._ensure_private_cache_root()
        token = self._cache_token(key)
        lock_path = self._cache_lock_path(token)
        flags = os.O_CREAT | os.O_RDWR
        if hasattr(os, "O_NOFOLLOW"):
            flags |= os.O_NOFOLLOW
        descriptor = os.open(lock_path, flags, 0o600)
        try:
            while True:
                try:
                    fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
                    break
                except BlockingIOError:
                    await asyncio.sleep(0.05)
            yield token
        finally:
            try:
                fcntl.flock(descriptor, fcntl.LOCK_UN)
            except Exception as error:
                logger.info(
                    "Incremental cache unlock unavailable; continuing: {}".format(error)
                )
            try:
                os.close(descriptor)
            except Exception as error:
                logger.info(
                    "Incremental cache lock close unavailable; continuing: {}".format(
                        error
                    )
                )

    async def _release_incremental_cache_best_effort(self, cache_context):
        try:
            await cache_context.__aexit__(None, None, None)
        except asyncio.CancelledError:
            raise
        except Exception as error:
            logger.info(
                "Incremental cache lock release unavailable; continuing: {}".format(
                    error
                )
            )
        try:
            # A post-release prune lets the last concurrent builder enforce
            # limits after entries skipped while their shard locks were held.
            self._prune_incremental_cache()
        except Exception as error:
            logger.info(
                "Incremental cache pruning unavailable; continuing: {}".format(error)
            )

    def _cache_entry(self, key):
        return self._cache_root / "artifacts" / self._cache_token(key)

    @staticmethod
    def _payload_specific_cache_path(relative):
        parts = pathlib.PurePath(relative).parts
        if not parts:
            return False
        project = parts[0].lower()
        return project == "athenacore" or project.startswith("agent.profiles.")

    @staticmethod
    def _cached_artifact_directories(root):
        root = pathlib.Path(root)
        for directory, children, _ in os.walk(root):
            directory = pathlib.Path(directory)
            for child in list(children):
                if child.lower() in {"bin", "obj"}:
                    yield directory / child
                    children.remove(child)

    def _restore_incremental_cache(self, key, workspace):
        entry = self._cache_entry(key)
        if not entry.is_dir():
            return False
        self._ensure_private_cache_directory(entry)
        for cached in entry.rglob("*"):
            metadata = cached.lstat()
            if (
                stat.S_ISLNK(metadata.st_mode)
                or metadata.st_uid != os.geteuid()
                or not (stat.S_ISDIR(metadata.st_mode) or stat.S_ISREG(metadata.st_mode))
            ):
                raise OSError("cache entry contains an unsafe filesystem object")
        workspace = pathlib.Path(workspace)
        staging = pathlib.Path(
            tempfile.mkdtemp(prefix=".athena-cache-restore-", dir=workspace)
        )
        installed = []
        try:
            for cached in entry.rglob("*"):
                if cached.is_file():
                    target = staging / cached.relative_to(entry)
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(cached, target)
            for source in list(self._cached_artifact_directories(staging)):
                relative = source.relative_to(staging)
                target = workspace / relative
                if target.exists():
                    continue
                target.parent.mkdir(parents=True, exist_ok=True)
                source.replace(target)
                installed.append(target)
            os.utime(entry, None)
            return True
        except Exception:
            for target in reversed(installed):
                shutil.rmtree(target, ignore_errors=True)
            raise
        finally:
            shutil.rmtree(staging, ignore_errors=True)

    def _restore_incremental_cache_best_effort(self, key, workspace):
        try:
            return self._restore_incremental_cache(key, workspace)
        except Exception as error:
            logger.info("Incremental cache restore unavailable; continuing clean: {}".format(error))
            return False

    def _save_incremental_cache(self, key, workspace):
        self._ensure_private_cache_root()
        entry = self._cache_entry(key)
        staging = pathlib.Path(
            tempfile.mkdtemp(prefix=entry.name + ".staging.", dir=entry.parent)
        )
        try:
            workspace = pathlib.Path(workspace)
            for root, directories, _ in os.walk(workspace):
                root_path = pathlib.Path(root)
                for directory in list(directories):
                    if directory.lower() not in {"bin", "obj"}:
                        continue
                    source = root_path / directory
                    if self._payload_specific_cache_path(
                        source.relative_to(workspace)
                    ):
                        directories.remove(directory)
                        continue
                    target = staging / source.relative_to(workspace)
                    shutil.copytree(
                        source,
                        target,
                        dirs_exist_ok=True,
                        ignore=shutil.ignore_patterns("publish"),
                    )
                    directories.remove(directory)
            staging.chmod(0o700)
            if self._directory_size(staging) > self._cache_byte_limit:
                return
            if entry.exists():
                shutil.rmtree(entry)
            staging.replace(entry)
        finally:
            if staging.exists():
                shutil.rmtree(staging)
        self._prune_incremental_cache()

    def _save_incremental_cache_best_effort(self, key, workspace):
        try:
            self._save_incremental_cache(key, workspace)
            return True
        except Exception as error:
            logger.info("Incremental cache save unavailable; continuing without cache: {}".format(error))
            return False

    @staticmethod
    def _owned_cache_directory_metadata(directory):
        metadata = os.lstat(directory)
        if (
            not stat.S_ISDIR(metadata.st_mode)
            or stat.S_ISLNK(metadata.st_mode)
            or metadata.st_uid != os.geteuid()
        ):
            raise OSError("cache directory is unsafe or foreign-owned")
        return metadata

    @classmethod
    def _directory_size(cls, directory):
        cls._owned_cache_directory_metadata(directory)
        total = 0
        for path in pathlib.Path(directory).rglob("*"):
            metadata = path.lstat()
            if stat.S_ISLNK(metadata.st_mode) or metadata.st_uid != os.geteuid():
                raise OSError("cache entry contains an unsafe filesystem object")
            if stat.S_ISREG(metadata.st_mode):
                total += metadata.st_size
            elif not stat.S_ISDIR(metadata.st_mode):
                raise OSError("cache entry contains an unsupported filesystem object")
        return total

    def _remove_cache_directory_if_unlocked(self, directory, token):
        lock_path = self._cache_lock_path(token)
        flags = os.O_CREAT | os.O_RDWR
        if hasattr(os, "O_NOFOLLOW"):
            flags |= os.O_NOFOLLOW
        descriptor = os.open(lock_path, flags, 0o600)
        try:
            lock_metadata = os.fstat(descriptor)
            if (
                not stat.S_ISREG(lock_metadata.st_mode)
                or lock_metadata.st_uid != os.geteuid()
            ):
                raise OSError("cache lock file is unsafe or foreign-owned")
            os.fchmod(descriptor, 0o600)
            try:
                fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                return False
            self._owned_cache_directory_metadata(directory)
            shutil.rmtree(directory, ignore_errors=True)
            return True
        finally:
            try:
                fcntl.flock(descriptor, fcntl.LOCK_UN)
            finally:
                os.close(descriptor)

    def _prune_incremental_cache(self):
        artifacts = self._cache_root / "artifacts"
        stale_before = time.time() - self._cache_staging_ttl_seconds
        for path in artifacts.iterdir():
            match = self._CACHE_STAGING_PATTERN.fullmatch(path.name)
            if not match:
                continue
            try:
                metadata = self._owned_cache_directory_metadata(path)
            except OSError:
                continue
            if metadata.st_mtime < stale_before:
                self._remove_cache_directory_if_unlocked(path, match.group(1))

        entries = []
        for path in artifacts.iterdir():
            if not self._CACHE_ENTRY_PATTERN.fullmatch(path.name):
                continue
            try:
                metadata = self._owned_cache_directory_metadata(path)
            except OSError:
                continue
            entries.append((path, metadata.st_mtime))
        entries.sort(key=lambda item: item[1], reverse=True)
        retained_size = 0
        victims = []
        for index, (entry, _) in enumerate(entries):
            entry_size = self._directory_size(entry)
            if (
                index >= self._cache_entry_limit
                or retained_size + entry_size > self._cache_byte_limit
            ):
                victims.append(entry)
            else:
                retained_size += entry_size

        for entry in victims:
            self._remove_cache_directory_if_unlocked(entry, entry.name)

    def returnSuccess(self, resp: BuildResponse, build_msg, agent_build_path, stdout) -> BuildResponse:
        resp.status = BuildStatus.Success
        resp.build_message = build_msg + self._total_build_timing()
        with open(f"{agent_build_path.name}/output.zip", "rb") as payload_file:
            resp.payload = payload_file.read()
        resp.set_build_stdout(stdout)
        return resp

    def returnFailure(self, resp: BuildResponse, err_msg, build_msg) -> BuildResponse:
        resp.status = BuildStatus.Error
        resp.payload = b""
        resp.build_message = build_msg + self._total_build_timing()
        resp.build_stderr = err_msg
        return resp

    def _total_build_timing(self):
        started = getattr(self, "_build_started", None)
        if started is None:
            return ""
        return "\nTotal build elapsed: {:.3f}s".format(time.monotonic() - started)

    def _format_process_failure(self, prefix, error, command):
        def decode(value):
            return value.decode(errors="replace") if isinstance(value, bytes) else str(value)

        diagnostics = []
        if isinstance(error, subprocess.CalledProcessError):
            if error.output:
                diagnostics.append(decode(error.output))
            if error.stderr:
                diagnostics.append(decode(error.stderr))
        else:
            value = error.args[0] if len(error.args) == 1 else error
            diagnostics.append(decode(value))
        if command:
            diagnostics.append(shlex.join(command))
        return prefix + ": " + "\n".join(diagnostics)

    async def _report_build_step(self, step_name, stdout, success):
        result = await SendMythicRPCPayloadUpdatebuildStep(
            MythicRPCPayloadUpdateBuildStepMessage(
                PayloadUUID=self.uuid,
                StepName=step_name,
                StepStdout=stdout,
                StepSuccess=success,
            )
        )
        if not result.Success:
            raise RuntimeError(
                "Failed to update Mythic build step: {}".format(
                    result.Error or "unknown RPC error"
                )
            )
        return result

    async def _return_step_failure(
        self, resp, step_name, build_message, error, command=None, timing=None
    ):
        if command is None:
            command = getattr(error, "cmd", getattr(error, "command", []))
        diagnostics = self._format_process_failure("Error building payload", error, command)
        if timing:
            diagnostics += "\n" + timing
        await self._report_build_step(step_name, diagnostics, False)
        return self.returnFailure(
            resp,
            diagnostics,
            build_message,
        )
    
    def getRid(self):
        if self.selected_os.upper() == "WINDOWS":
            return "win-" + self.get_parameter("arch")
        elif self.selected_os.upper() == "LINUX":
            return "linux-" + self.get_parameter("arch")
        elif self.selected_os.upper() == "MACOS":
                return "osx-" + self.get_parameter("arch")
        elif self.selected_os.upper() == "REDHAT":
            return "rhel-x64"
        
    def updateRootsFile(self, agent_build_path, roots_replace):
        roots_path = os.path.join(agent_build_path.name, "AthenaCore", "Roots.xml")
        with open(roots_path, "r") as roots_file:
            roots = roots_file.read()
        with open(roots_path, "w") as roots_file:
            roots_file.write(roots.replace("<!-- {{REPLACEME}} -->", roots_replace))

    def getBuildCommand(self, rid):
        assembly_name = self._validated_assembly_name()
        crypto_provider = self._validated_crypto_provider(
            getattr(self, "_crypto_provider", "Aes")
        )
        return [
            "dotnet", "publish", "AthenaCore",
            "-r", rid,
            "-c", str(self.get_parameter("configuration")),
            "--nologo",
            "--self-contained={}".format(self.get_parameter("self-contained")),
            "/p:PublishSingleFile={}".format(self.get_parameter("single-file")),
            "/p:IncludeNativeLibrariesForSelfExtract={}".format(
                self.get_parameter("single-file")
            ),
            "/p:EnableCompressionInSingleFile={}".format(self.get_parameter("compressed")),
            "/p:PublishTrimmed={}".format(self.get_parameter("trimmed")),
            "/p:Obfuscate=False",
            "/p:PublishAOT=False",
            "/p:DebugType=None",
            "/p:DebugSymbols=false",
            "/p:PluginsOnly=false",
            "/p:HandlerOS={}".format(self.selected_os.lower()),
            "/p:CryptoProvider={}".format(crypto_provider),
            "/p:UseSystemResourceKeys={}".format(self.get_parameter("usesystemresourcekeys")),
            "/p:InvariantGlobalization={}".format(self.get_parameter("invariantglobalization")),
            "/p:StackTraceSupport={}".format(self.get_parameter("stacktracesupport")),
            "/p:PayloadUUID={}".format(self.uuid),
            "/p:WindowsService={}".format(
                self.get_parameter("output-type") == "windows service"
            ),
            "/p:RandomName={}".format(assembly_name),
        ]

    def _custom_obfuscator_binary(self, workspace):
        return os.path.join(
            workspace, "Obfuscator", "bin", "Release", "net10.0",
            "obfuscator.dll",
        )

    async def _ensure_custom_obfuscator(self, workspace):
        binary = self._custom_obfuscator_binary(workspace)
        if not os.path.isfile(binary):
            project = os.path.join(workspace, "Obfuscator", "Obfuscator.csproj")
            await self._run_checked(
                ["dotnet", "build", project, "-c", "Release", "--nologo"],
                workspace,
            )
        if not os.path.isfile(binary):
            raise FileNotFoundError("Custom obfuscator build produced no binary: " + binary)
        return binary

    async def rewrite_payload_source(self, workspace):
        binary = await self._ensure_custom_obfuscator(workspace)
        seed = derive_obfuscation_seed(self.uuid)
        crypto_provider = self._validated_crypto_provider(self._crypto_provider)
        await self._run_checked(
            [
                "dotnet", binary, "rewrite-source",
                "--seed", str(seed),
                "--uuid", self.uuid,
                "--input", workspace,
                "--output", workspace,
                "--map", self._obfuscation_map_path(workspace, "source"),
                "--broad-semantic-rename",
                "--project-root", "AthenaCore/AthenaCore.csproj",
                "--configuration", str(self.get_parameter("configuration")),
                "--handler-os", self.selected_os.lower(),
                "--crypto-provider", crypto_provider,
            ],
            workspace,
        )

    @staticmethod
    def _obfuscation_map_path(workspace, phase):
        private_directory = pathlib.Path(workspace) / ".athena-private"
        private_directory.mkdir(parents=True, exist_ok=True, mode=0o700)
        private_directory.chmod(0o700)
        return str(private_directory / (phase + "-obf-map.json"))

    async def obfuscate_published_assemblies(self, agent_build_path, output_path):
        binary = await self._ensure_custom_obfuscator(agent_build_path.name)
        first_party_assemblies = self._first_party_assembly_names(
            agent_build_path.name
        )
        command = [
            "dotnet", binary, "rewrite-il-batch",
            "--seed", str(derive_obfuscation_seed(self.uuid)),
            "--dir", output_path,
            "--map", self._obfuscation_map_path(agent_build_path.name, "il"),
        ]
        for assembly_name in first_party_assemblies:
            command.extend(["--first-party-assembly", assembly_name])
        if self.get_parameter("single-file"):
            command.extend(["--skip-file-rename", "--skip-assembly-rename"])
        await self._run_checked(command, agent_build_path.name)

    def _first_party_assembly_names(self, workspace):
        workspace = pathlib.Path(workspace)
        manager = (
            "Agent.Managers.Windows"
            if self.selected_os.lower() == "windows"
            else "Agent.Managers.Linux"
        )
        project_paths = {
            pathlib.Path("Agent.Models/Agent.Models.csproj"),
            pathlib.Path("Agent.Managers.Reflection/Agent.Managers.Reflection.csproj"),
            pathlib.Path("Agent.Managers.Python/Agent.Managers.Python.csproj"),
            pathlib.Path(manager) / (manager + ".csproj"),
            pathlib.Path("Agent.Crypto.{0}/Agent.Crypto.{0}.csproj".format(
                self._validated_crypto_provider(getattr(self, "_crypto_provider", "Aes"))
            )),
        }
        for reference in getattr(self, "_project_references", []):
            normalized = self._normalized_reference(reference)
            project_paths.add(pathlib.Path(normalized))

        identities = {self._validated_assembly_name()}
        for relative_project in project_paths:
            identities.add(effective_assembly_name(workspace / relative_project))
        return sorted(identities, key=str.casefold)

    async def _configure_tasks(self, agent_build_path, roots_replace):
        unloadable_commands = plugin_utilities.get_unloadable_commands()
        command_projects = []
        for command_name in self.commands.get_commands():
            if command_name in unloadable_commands:
                continue
            if command_name == "nidhogg":
                for command in plugin_utilities.get_nidhogg_commands():
                    self.commands.add_command(command)
            if command_name == "ds":
                if self.selected_os.lower() == "redhat":
                    continue
                for command in plugin_utilities.get_ds_commands():
                    self.commands.add_command(command)
            if command_name == "coff":
                for command in plugin_utilities.get_coff_commands():
                    self.commands.add_command(command)
            if command_name == "inject-shellcode":
                for command in plugin_utilities.get_inject_shellcode_commands():
                    self.commands.add_command(command)

            command_projects.append(os.path.join(command_name, "{}.csproj".format(command_name)))
            roots_replace += '<assembly fullname="{}"/>\n'.format(command_name)

        if command_projects:
            await self.addCommands(agent_build_path, command_projects)
        self._write_project_references(agent_build_path.name, self._project_references)
        self.updateRootsFile(agent_build_path, roots_replace)


    async def _gather_files(self, resp, agent_build_path, cache_key=None):
        started = time.monotonic()
        cache_status = "disabled"
        try:
            self._copy_filtered_source(self.agent_code_path, agent_build_path.name)
            if cache_key is not None:
                cache_hit = self._restore_incremental_cache_best_effort(
                    cache_key, agent_build_path.name
                )
                cache_status = "hit" if cache_hit else "miss"
        except Exception as error:
            return await self._return_step_failure(
                resp,
                "Gather Files",
                "Error occurred while gathering payload files. Check stderr for more information.",
                error,
                timing="Gather Files elapsed: {:.3f}s".format(time.monotonic() - started),
            )
        await self._report_build_step(
            "Gather Files",
            "Successfully created temporary directory at {}\n"
            "Incremental cache: {}\nGather Files elapsed: {:.3f}s".format(
                agent_build_path.name, cache_status, time.monotonic() - started
            ),
            True,
        )

    async def _configure_profiles(self, resp, agent_build_path):
        started = time.monotonic()
        roots = ""
        agent_parameters = {}
        for c2 in self.c2info:
            try:
                profile_name = c2.get_c2profile()["name"]
                if profile_name not in self.PROFILE_METADATA:
                    raise ValueError(
                        "Unsupported C2 profile type for Athena: {}".format(profile_name)
                    )
                agent_parameters = c2.get_parameters_dict()
                roots += '<assembly fullname="{}"/>\n'.format(
                    self.PROFILE_METADATA[profile_name]["root"]
                )
                await self.buildProfile(agent_build_path, c2, profile_name)
            except asyncio.CancelledError:
                raise
            except Exception as error:
                failure = await self._return_step_failure(
                    resp,
                    "Configure C2 Profiles",
                    "Error occurred while configuring C2 profiles. Check stderr for more information.",
                    error,
                    timing="Profile config elapsed: {:.3f}s".format(
                        time.monotonic() - started
                    ),
                )
                return failure, roots, agent_parameters

        await self._report_build_step(
            "Configure C2 Profiles",
            "Successfully configured c2 profiles and added to agent\nProfile config elapsed: {:.3f}s".format(
                time.monotonic() - started
            ),
            True,
        )
        return None, roots, agent_parameters

    async def _configure_agent(self, resp, agent_build_path, agent_parameters):
        started = time.monotonic()
        try:
            await self.buildConfig(agent_build_path, agent_parameters)
        except asyncio.CancelledError:
            raise
        except Exception as error:
            return await self._return_step_failure(
                resp,
                "Configure Agent",
                "Error occurred while configuring the agent. Check stderr for more information.",
                error,
                timing="Agent config elapsed: {:.3f}s".format(time.monotonic() - started),
            )
        await self._report_build_step(
            "Configure Agent",
            "Successfully replaced agent configuration\nAgent config elapsed: {:.3f}s".format(
                time.monotonic() - started
            ),
            True,
        )

    async def _add_tasks(self, resp, agent_build_path, roots):
        started = time.monotonic()
        try:
            await self._configure_tasks(agent_build_path, roots)
        except asyncio.CancelledError:
            raise
        except Exception as error:
            return await self._return_step_failure(
                resp,
                "Add Tasks",
                "Error occurred while adding tasks. Check stderr for more information.",
                error,
                timing="Project-reference generation/tasks elapsed: {:.3f}s".format(
                    time.monotonic() - started
                ),
            )
        await self._report_build_step(
            "Add Tasks",
            "Successfully added tasks to agent\nProject-reference generation/tasks elapsed: {:.3f}s".format(
                time.monotonic() - started
            ),
            True,
        )

    async def _compile(self, resp, agent_build_path, rid, assembly_name):
        command = self.getBuildCommand(rid)
        if self.get_parameter("trimmed") == True:
            command.append("/p:OptimizationPreference=Size")
        output_path = "{}/AthenaCore/bin/{}/net10.0/{}/publish/".format(
            agent_build_path.name,
            self.get_parameter("configuration").capitalize(),
            rid,
        )

        logger.info("Executing Command: " + shlex.join(command))
        publish_started = time.monotonic()
        try:
            build_stdout, build_stderr = await self._run_checked(
                command, agent_build_path.name
            )
        except asyncio.CancelledError:
            raise
        except Exception as error:
            failure = await self._return_step_failure(
                resp,
                "Compile",
                "Error occurred while building payload. Check stderr for more information.",
                error,
                command,
                "Publish elapsed: {:.3f}s".format(time.monotonic() - publish_started),
            )
            return failure, None, None
        publish_timing = "Publish elapsed: {:.3f}s".format(
            time.monotonic() - publish_started
        )

        logger.critical("stdout: " + str(build_stdout))
        logger.critical("stderr: " + str(build_stderr))
        sys.stdout.flush()

        obfuscation_timing = None
        if self.get_parameter("obfuscate"):
            obfuscation_started = time.monotonic()
            try:
                await self.obfuscate_published_assemblies(agent_build_path, output_path)
            except asyncio.CancelledError:
                raise
            except Exception as error:
                failure = await self._return_step_failure(
                    resp,
                    "Compile",
                    "Error occurred while obfuscating payload assemblies. Check stderr for more information.",
                    error,
                    timing=publish_timing + "\nObfuscation elapsed: {:.3f}s".format(
                        time.monotonic() - obfuscation_started
                    ),
                )
                return failure, None, None
            obfuscation_timing = "\nObfuscation elapsed: {:.3f}s".format(
                time.monotonic() - obfuscation_started
            )

        if (
            self.selected_os.lower() == "windows"
            and self.get_parameter("configuration") != "Debug"
        ):
            try:
                self.prepareWinExe(output_path)
            except Exception as error:
                failure = await self._return_step_failure(
                    resp,
                    "Compile",
                    "Error occurred while preparing the compiled payload. Check stderr for more information.",
                    error,
                    timing=publish_timing,
                )
                return failure, None, None

        if self.get_parameter("output-type") == "app bundle":
            try:
                mac_bundler.create_app_bundle(
                    "Agent",
                    os.path.join(output_path, assembly_name),
                    output_path,
                )
                os.remove(os.path.join(output_path, assembly_name))
            except Exception as error:
                failure = await self._return_step_failure(
                    resp,
                    "Compile",
                    "Error occurred while creating the app bundle. Check stderr for more information.",
                    error,
                    timing=publish_timing,
                )
                return failure, None, None

        await self._report_build_step(
            "Compile",
            "Successfully compiled payload\n{}{}".format(
                publish_timing, obfuscation_timing or ""
            ),
            True,
        )
        return None, output_path, build_stdout

    async def _package(self, resp, agent_build_path, source_path, stdout, source_export):
        started = time.monotonic()
        try:
            private_metadata = pathlib.Path(source_path) / ".athena-private"
            if private_metadata.exists():
                shutil.rmtree(private_metadata)
            for root, directories, files in os.walk(source_path):
                directories.sort()
                for filename in sorted(files):
                    lowered = filename.lower()
                    if "obf" in lowered and "map" in lowered:
                        pathlib.Path(root, filename).unlink()
            shutil.make_archive(
                os.path.join(agent_build_path.name, "output"),
                "zip",
                source_path,
            )
        except Exception as error:
            return await self._return_step_failure(
                resp,
                "Zip",
                "Error occurred while zipping the payload. Check stderr for more information.",
                error,
                timing="Packaging elapsed: {:.3f}s".format(time.monotonic() - started),
            )

        await self._report_build_step(
            "Zip",
            ("Successfully zipped source payload" if source_export else "Successfully zipped payload")
            + "\nPackaging elapsed: {:.3f}s".format(time.monotonic() - started),
            True,
        )
        return self.returnSuccess(
            resp,
            "File built succesfully!",
            agent_build_path,
            stdout,
        )

    async def build(self) -> BuildResponse:
        resp = BuildResponse(status=BuildStatus.Error)
        agent_build_path = None
        cache_context = None
        cache_key = None
        try:
            self._build_started = time.monotonic()
            assembly_name = self._validated_assembly_name()
            self._project_references = []
            self._crypto_provider = self._configured_crypto_provider()

            if (
                self.get_parameter("output-type") == "app bundle"
                and self.selected_os.upper() != "MACOS"
            ):
                return self.returnFailure(
                    resp,
                    "Error building payload: App Bundles are only supported on MacOS",
                    "Error occurred while building payload. Check stderr for more information.",
                )
            if (
                self.get_parameter("output-type") == "windows service"
                and self.get_parameter("obfuscate") == True
            ):
                return self.returnFailure(
                    resp,
                    "Error building payload: Windows service's obfuscation is not supported yet.",
                    "Error occurred while building payload. Check stderr for more information.",
                )

            try:
                if self.get_parameter("obfuscate"):
                    logger.info(
                        "Incremental cache disabled for randomized obfuscated builds"
                    )
                else:
                    cache_key = self._structural_cache_key()
                    cache_context = self._incremental_cache_guard(cache_key)
                    await cache_context.__aenter__()
            except asyncio.CancelledError:
                raise
            except Exception as error:
                logger.info(
                    "Incremental cache unavailable; continuing clean: {}".format(error)
                )
                cache_context = None
                cache_key = None
            agent_build_path = tempfile.TemporaryDirectory(prefix="athena-build-")
            os.chmod(agent_build_path.name, 0o700)

            failure = await self._gather_files(resp, agent_build_path, cache_key)
            if failure:
                return failure

            failure, roots, agent_parameters = await self._configure_profiles(
                resp, agent_build_path
            )
            if failure:
                return failure

            failure = await self._configure_agent(
                resp, agent_build_path, agent_parameters
            )
            if failure:
                return failure

            rid = self.getRid()
            failure = await self._add_tasks(resp, agent_build_path, roots)
            if failure:
                return failure

            if self.get_parameter("obfuscate"):
                await self.rewrite_payload_source(agent_build_path.name)

            if self.get_parameter("output-type") == "source":
                return await self._package(
                    resp,
                    agent_build_path,
                    agent_build_path.name,
                    "Source Exported",
                    True,
                )

            failure, output_path, build_stdout = await self._compile(
                resp, agent_build_path, rid, assembly_name
            )
            if failure:
                return failure
            if cache_key is not None:
                self._save_incremental_cache_best_effort(
                    cache_key, agent_build_path.name
                )
            return await self._package(
                resp,
                agent_build_path,
                output_path,
                build_stdout,
                False,
            )
        except asyncio.CancelledError:
            raise
        except Exception:
            return self.returnFailure(
                resp,
                str(traceback.format_exc()),
                "Exception in builder.py",
            )
        finally:
            cleanup = getattr(agent_build_path, "cleanup", None)
            if cleanup is not None:
                try:
                    cleanup()
                except Exception as error:
                    logger.info(
                        "Failed to clean temporary build directory: {}".format(error)
                    )
            if cache_context is not None:
                await self._release_incremental_cache_best_effort(cache_context)
