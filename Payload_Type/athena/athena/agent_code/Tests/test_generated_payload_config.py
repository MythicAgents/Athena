import ast
import importlib.util
import json
import re
import shutil
import subprocess
from datetime import date
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest import TestCase


AGENT_CODE = Path(__file__).parents[1]
GENERATOR_PATH = AGENT_CODE / ".." / "mythic" / "agent_functions" / "config_generator.py"


def load_generator():
    spec = importlib.util.spec_from_file_location("repo_config_generator", GENERATOR_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def decode_generated_source(source):
    key = int(re.search(r"private static readonly byte _k = 0x([0-9A-F]{2});", source).group(1), 16)
    data_section = source.split("private static readonly byte _k")[0]
    data = bytes(int(value, 16) for value in re.findall(r"0x([0-9A-F]{2})", data_section))
    return json.loads(bytes(value ^ key for value in data).decode("utf-8"))


class GeneratedPayloadConfigTests(TestCase):
    profile_samples = {
        "http": {
            "callback_host": "https://example.test",
            "callback_port": "443",
            "get_uri": "index",
            "post_uri": "submit",
            "query_path_name": "q",
            "proxy_host": "",
            "proxy_port": "",
            "proxy_user": "",
            "proxy_pass": "",
            "headers": {"User-Agent": "Athena", "X-Test": "quoted value"},
            "encrypted_exchange_check": "T",
        },
        "websocket": {
            "callback_host": "wss://example.test",
            "callback_port": "443",
            "ENDPOINT_REPLACE": "socket",
            "USER_AGENT": "Athena",
            "domain_front": "front.example.test",
            "encrypted_exchange_check": "F",
        },
        "smb": {"pipename": "athena-pipe", "encrypted_exchange_check": True},
        "discord": {"discord_token": "discord-test-value", "bot_channel": "1234", "encrypted_exchange_check": False},
        "github": {
            "personal_access_token": "github-test-value",
            "github_username": "operator",
            "github_repo": "repo",
            "server_issue_number": "10",
            "client_issue_number": 11,
            "encrypted_exchange_check": "T",
        },
        "zoom": {
            "zoom_account_id": "account",
            "client_id": "client",
            "client_secret": "zoom-test-value",
            "channel_id": "channel",
            "encrypted_exchange_check": "F",
        },
    }

    expected_profile_configs = {
        "http": {
            "callback_host": "https://example.test", "callback_port": "443", "get_uri": "index",
            "post_uri": "submit", "query_path_name": "q", "proxy_host": "", "proxy_port": "",
            "proxy_user": "", "proxy_pass": "", "headers": {"User-Agent": "Athena", "X-Test": "quoted value"},
            "encrypted_exchange_check": True,
        },
        "websocket": {
            "callback_host": "wss://example.test", "callback_port": "443", "ENDPOINT_REPLACE": "socket",
            "USER_AGENT": "Athena", "domain_front": "front.example.test", "encrypted_exchange_check": False,
        },
        "smb": {"pipename": "athena-pipe", "encrypted_exchange_check": True},
        "discord": {"discord_token": "discord-test-value", "bot_channel": "1234", "encrypted_exchange_check": False},
        "github": {
            "personal_access_token": "github-test-value", "github_username": "operator", "github_repo": "repo",
            "server_issue_number": "10", "client_issue_number": 11, "encrypted_exchange_check": True,
        },
        "zoom": {
            "account_id": "account", "client_id": "client", "client_secret": "zoom-test-value",
            "channel_id": "channel", "encrypted_exchange_check": False, "user_id": "me",
            "api_base": "https://api.zoom.us/v2", "oauth_base": "https://zoom.us/oauth",
        },
    }

    def test_profile_source_round_trips_structured_values(self):
        generator = load_generator()
        config = {"callback_host": "https://example.test", "callback_port": 443, "headers": {"User-Agent": "Athena", "X-Test": "quoted \"value\""}, "encrypted_exchange_check": True}
        source = generator.render_xor_config("Agent.Profiles", "ChannelConfig", config, xor_key=0x5A)
        self.assertEqual(config, decode_generated_source(source))
        self.assertIn("namespace Agent.Profiles", source)
        self.assertNotIn("https://example.test", source)

    def test_profile_parameters_normalize_mythic_boolean_and_exclude_psk(self):
        generator = load_generator()
        normalized = generator.normalize_profile_config({"AESPSK": {"enc_key": "secret"}, "encrypted_exchange_check": "T", "callback_port": 443})
        self.assertEqual({"encrypted_exchange_check": True, "callback_port": 443}, normalized)

    def test_agent_configuration_includes_payload_uuid_and_crypto_selection(self):
        generator = load_generator()
        config, crypto = generator.normalize_agent_config("37eb846a-12b9-45d5-a49c-8e10754cc0ba", {"AESPSK": {"enc_key": "base64-key"}, "callback_interval": "15", "callback_jitter": "25", "killdate": "2030-01-01"})
        self.assertEqual("Aes", crypto)
        self.assertEqual("37eb846a-12b9-45d5-a49c-8e10754cc0ba", config["uuid"])
        self.assertEqual(15, config["callback_interval"])
        self.assertEqual(25, config["callback_jitter"])
        self.assertEqual("base64-key", config["psk"])

    def test_zoom_is_registered_with_its_namespace_and_directory(self):
        generator = load_generator()
        self.assertEqual(("Agent.Profiles.Zoom", "Agent.Profiles"), generator.PROFILE_CONFIG_TARGETS["zoom"])

    def test_zoom_legacy_account_name_and_defaults_are_normalized(self):
        generator = load_generator()
        config = generator.normalize_profile_config(
            {"zoom_account_id": "acct", "client_id": "client", "client_secret": "secret", "channel_id": "channel"},
            profile_name="zoom",
        )
        self.assertEqual("acct", config["account_id"])
        self.assertEqual("me", config["user_id"])
        self.assertEqual("https://api.zoom.us/v2", config["api_base"])
        self.assertEqual("https://zoom.us/oauth", config["oauth_base"])
        self.assertNotIn("zoom_account_id", config)

    def test_write_profile_config_targets_copied_profile_directory(self):
        generator = load_generator()
        with TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "Agent.Profiles.Zoom").mkdir()
            output = generator.write_profile_config(root, "zoom", {"account_id": "account", "client_secret": "secret"}, xor_key=0x23)
            self.assertEqual(root / "Agent.Profiles.Zoom" / "ChannelConfig.cs", output)
            self.assertEqual("account", decode_generated_source(output.read_text())["account_id"])
            self.assertEqual("me", decode_generated_source(output.read_text())["user_id"])


    def test_all_supported_profiles_round_trip_generated_configuration(self):
        generator = load_generator()
        with TemporaryDirectory() as directory:
            root = Path(directory)
            for profile_name, parameters in self.profile_samples.items():
                profile_directory = generator.PROFILE_CONFIG_TARGETS[profile_name][0]
                (root / profile_directory).mkdir()
                with self.subTest(profile=profile_name):
                    output = generator.write_profile_config(root, profile_name, parameters, xor_key=0x4D)
                    self.assertEqual(
                        self.expected_profile_configs[profile_name],
                        decode_generated_source(output.read_text()),
                    )

    def test_generated_csharp_decoders_and_typed_options_round_trip(self):
        generator = load_generator()
        option_types = {
            "http": ("Http", "Agent.Profiles", "HttpChannelOptions", "HttpChannelOptionsJsonContext"),
            "websocket": ("Websocket", "Agent.Profiles.Websocket", "WebsocketChannelOptions", "WebsocketChannelOptionsJsonContext"),
            "smb": ("Smb", "Agent.Profiles.Smb", "SmbChannelOptions", "SmbChannelOptionsJsonContext"),
            "discord": ("Discord", "Agent.Profiles", "DiscordChannelOptions", "DiscordChannelOptionsJsonContext"),
            "github": ("GitHub", "Agent.Profiles", "GitHubChannelOptions", "GitHubChannelOptionsJsonContext"),
            "zoom": ("Zoom", "Agent.Profiles", "ZoomChannelOptions", "ZoomChannelOptionsJsonContext"),
        }
        with TemporaryDirectory() as directory:
            root = Path(directory)
            for profile_name, parameters in self.profile_samples.items():
                source_directory, namespace, options_type, context_type = option_types[profile_name]
                project = root / profile_name
                project.mkdir()
                generated_root = project / "generated"
                (generated_root / generator.PROFILE_CONFIG_TARGETS[profile_name][0]).mkdir(parents=True)
                config_path = generator.write_profile_config(
                    generated_root, profile_name, parameters, xor_key=0x6B
                )
                options_path = AGENT_CODE / f"Agent.Profiles.{source_directory}" / f"{options_type}.cs"
                (project / f"{options_type}.cs").write_text(options_path.read_text())
                (project / "ConfigSmoke.csproj").write_text(
                    '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                    '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
                    '<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>'
                    '</PropertyGroup></Project>'
                )
                (project / "Program.cs").write_text(
                    "using System.Text.Json;\n"
                    f"using {namespace};\n"
                    f"var options = JsonSerializer.Deserialize(ChannelConfig.Decode(), "
                    f"{context_type}.Default.{options_type}) ?? throw new Exception();\n"
                    f"Console.Write(JsonSerializer.Serialize(options, {context_type}.Default.{options_type}));\n"
                )
                with self.subTest(profile=profile_name):
                    result = subprocess.run(
                        ["dotnet", "run", "--project", str(project / "ConfigSmoke.csproj"), "-c", "Release"],
                        check=False,
                        capture_output=True,
                        text=True,
                    )
                    self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                    expected = self.expected_profile_configs[profile_name]
                    actual = json.loads(result.stdout)
                    for key, value in expected.items():
                        if key in ("callback_port", "server_issue_number", "client_issue_number"):
                            value = int(value)
                        self.assertEqual(value, actual[key])

    def test_generated_zoom_config_reaches_profile_constructor(self):
        generator = load_generator()
        with TemporaryDirectory() as directory:
            root = Path(directory)
            shutil.copytree(AGENT_CODE / "Agent.Profiles.Zoom", root / "Agent.Profiles.Zoom")
            shutil.copytree(AGENT_CODE / "Agent.Models", root / "Agent.Models")
            expected = {
                "accountId": "account", "clientId": "client", "clientSecret": "zoom-test-value",
                "userId": "user", "channelId": "channel", "apiBase": "https://api.test/v2",
                "oauthBase": "https://oauth.test",
            }
            generator.write_profile_config(
                root,
                "zoom",
                {
                    "account_id": expected["accountId"], "client_id": expected["clientId"],
                    "client_secret": expected["clientSecret"], "user_id": expected["userId"],
                    "channel_id": expected["channelId"], "api_base": expected["apiBase"],
                    "oauth_base": expected["oauthBase"], "encrypted_exchange_check": False,
                },
                xor_key=0x45,
            )
            profile_project = root / "Agent.Profiles.Zoom" / "Agent.Profiles.Zoom.csproj"
            build = subprocess.run(
                ["dotnet", "build", str(profile_project), "-c", "Release", "--nologo", "--property", "WarningLevel=0", "/clp:ErrorsOnly"],
                check=False, capture_output=True, text=True,
            )
            self.assertEqual(0, build.returncode, build.stdout + build.stderr)
            assembly = root / "Agent.Profiles.Zoom" / "bin" / "Release" / "net8.0" / "Agent.Profiles.Zoom.dll"
            runner = root / "runner"
            runner.mkdir()
            (runner / "Runner.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
                '<Nullable>enable</Nullable></PropertyGroup></Project>'
            )
            (runner / "Program.cs").write_text(
                'using System.Reflection; using System.Text.Json; '
                'var a = Assembly.LoadFrom(args[0]); var t = a.GetType("Agent.Profiles.ZoomProfile")!; '
                'var p = Activator.CreateInstance(t, new object?[] { null, null, null, null })!; '
                'var names = new[] { "accountId", "clientId", "clientSecret", "userId", "channelId", "apiBase", "oauthBase" }; '
                'var values = names.ToDictionary(n => n, n => (string)t.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(p)!); '
                'Console.Write(JsonSerializer.Serialize(values));'
            )
            run = subprocess.run(
                ["dotnet", "run", "--project", str(runner / "Runner.csproj"), "-c", "Release", "--", str(assembly)],
                check=False, capture_output=True, text=True,
            )
            self.assertEqual(0, run.returncode, run.stdout + run.stderr)
            self.assertEqual(expected, json.loads(run.stdout))

    def test_generated_agent_config_is_consumed_by_csharp_runtime(self):
        generator = load_generator()
        with TemporaryDirectory() as directory:
            project = Path(directory)
            generated_root = project / "generated"
            (generated_root / "AthenaCore" / "Config").mkdir(parents=True)
            output, crypto = generator.write_agent_config(
                generated_root,
                "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
                {"AESPSK": {"enc_key": "test-key"}, "callback_interval": "15", "callback_jitter": 25, "killdate": "2030-01-02"},
                xor_key=0x37,
            )
            self.assertTrue(output.exists())
            self.assertEqual("Aes", crypto)
            source_files = {
                AGENT_CODE / "AthenaCore" / "Config" / "AgentConfig.cs": "AgentConfig.cs",
                AGENT_CODE / "AthenaCore" / "Config" / "AgentConfigOptions.cs": "AgentConfigOptions.cs",
                AGENT_CODE / "Agent.Models" / "Interfaces" / "IAgentConfig.cs": "IAgentConfig.cs",
            }
            for source, destination in source_files.items():
                (project / destination).write_text(source.read_text())
            (project / "ConfigSmoke.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
                '<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>'
                '</PropertyGroup></Project>'
            )
            (project / "Program.cs").write_text(
                'using Agent.Config; using System.Text.Json; '
                'var c = new AgentConfig(); '
                'Console.Write(JsonSerializer.Serialize(new { c.uuid, c.build_uuid, c.psk, c.sleep, c.jitter, killDate = c.killDate.ToString("yyyy-MM-dd") }));'
            )
            result = subprocess.run(
                ["dotnet", "run", "--project", str(project / "ConfigSmoke.csproj"), "-c", "Release"],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertEqual(
                {
                    "uuid": "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
                    "build_uuid": "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
                    "psk": "test-key",
                    "sleep": 15,
                    "jitter": 25,
                    "killDate": "2030-01-02",
                },
                json.loads(result.stdout),
            )

            _, crypto = generator.write_agent_config(
                generated_root,
                "no-key-payload",
                {"AESPSK": {"enc_key": None}, "killdate": "invalid"},
                xor_key=0x38,
            )
            self.assertEqual("None", crypto)
            fallback_result = subprocess.run(
                ["dotnet", "run", "--project", str(project / "ConfigSmoke.csproj"), "-c", "Release"],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(0, fallback_result.returncode, fallback_result.stdout + fallback_result.stderr)
            fallback = json.loads(fallback_result.stdout)
            self.assertEqual("no-key-payload", fallback["uuid"])
            self.assertEqual("", fallback["psk"])
            self.assertEqual(60, fallback["sleep"])
            self.assertEqual(10, fallback["jitter"])
            fallback_days = (date.fromisoformat(fallback["killDate"]) - date.today()).days
            self.assertIn(fallback_days, (364, 365, 366))

    def test_agent_configuration_defaults_to_no_crypto_without_a_key(self):
        generator = load_generator()
        config, crypto = generator.normalize_agent_config("payload-id", {"AESPSK": {"enc_key": None}})
        self.assertEqual("None", crypto)
        self.assertEqual("", config["psk"])
        self.assertEqual(60, config["callback_interval"])
        self.assertEqual(10, config["callback_jitter"])

    def test_invalid_xor_keys_are_rejected(self):
        generator = load_generator()
        for invalid_key in (0, 256):
            with self.subTest(xor_key=invalid_key):
                with self.assertRaises(ValueError):
                    generator.render_xor_config("Agent.Config", "Config", {}, xor_key=invalid_key)

    def test_unknown_profile_is_rejected(self):
        generator = load_generator()
        with TemporaryDirectory() as directory:
            with self.assertRaises(KeyError):
                generator.write_profile_config(directory, "unsupported", {})


class ProfileSourceMigrationTests(TestCase):
    profile_sources = {
        "Http": "HttpProfile.cs",
        "Smb": "SmbProfile.cs",
        "Websocket": "WebsocketProfile.cs",
        "Discord": "DiscordProfile.cs",
        "GitHub": "GitHubProfile.cs",
        "Zoom": "ZoomProfile.cs",
    }

    def test_checked_in_profile_configs_match_runtime_option_schemas(self):
        for directory in self.profile_sources:
            profile_dir = AGENT_CODE / f"Agent.Profiles.{directory}"
            option_source = next(profile_dir.glob("*ChannelOptions.cs")).read_text()
            expected_keys = set(re.findall(r'JsonPropertyName\("([^"]+)"\)', option_source))
            actual_keys = set(decode_generated_source((profile_dir / "ChannelConfig.cs").read_text()))
            with self.subTest(profile=directory):
                self.assertEqual(expected_keys, actual_keys)

    def test_profiles_use_generated_structured_configuration(self):
        for directory, source_name in self.profile_sources.items():
            profile_dir = AGENT_CODE / f"Agent.Profiles.{directory}"
            with self.subTest(profile=directory):
                self.assertFalse((profile_dir / "Base.txt").exists())
                self.assertIn("ChannelConfig.Decode()", (profile_dir / source_name).read_text())
                self.assertTrue(any(profile_dir.glob("*ChannelOptions.cs")))

    def test_agent_config_uses_generated_structured_configuration(self):
        source = (AGENT_CODE / "AthenaCore" / "Config" / "AgentConfig.cs").read_text()
        self.assertIn("AgentConfigData.Decode()", source)
        self.assertNotIn('"%UUID%"', source)

    def test_athena_core_exposes_zoom_local_debug_configuration(self):
        project = (AGENT_CODE / "AthenaCore" / "AthenaCore.csproj").read_text()
        self.assertIn("LocalDebugZoom", project)

    def test_local_debug_excludes_tool_and_test_projects(self):
        project = (AGENT_CODE / "AthenaCore" / "AthenaCore.csproj").read_text()
        self.assertIn(r"..\Tools\**\*.csproj", project)
        self.assertIn(r"..\Tests\**\*.csproj", project)

    def test_builder_uses_one_profile_configuration_path(self):
        source = GENERATOR_PATH.with_name("builder.py").read_text()
        self.assertIn("write_profile_config", source)
        self.assertNotIn("def buildZoom", source)
        self.assertNotIn("Base.txt", source)


    def test_builder_profile_and_agent_configuration_forwarding(self):
        tree = ast.parse(GENERATOR_PATH.with_name("builder.py").read_text())
        builder_class = next(node for node in tree.body if isinstance(node, ast.ClassDef) and node.name == "athena")
        methods = {node.name: node for node in builder_class.body if isinstance(node, ast.FunctionDef)}
        calls = []

        def write_profile_config(path, profile_name, parameters):
            calls.append(("profile", path, profile_name, parameters))

        def write_agent_config(path, payload_uuid, parameters):
            calls.append(("agent", path, payload_uuid, parameters))
            return path, "Aes"

        namespace = {"write_profile_config": write_profile_config, "write_agent_config": write_agent_config}
        exec(compile(ast.Module(body=[methods["buildProfile"], methods["buildConfig"]], type_ignores=[]), "builder-methods", "exec"), namespace)

        class C2:
            def get_parameters_dict(self):
                return {"value": "forwarded"}

        class BuildPath:
            name = "/tmp/copied-agent"

        class Builder:
            uuid = "payload-uuid"
            PROFILE_PROJECTS = {"http": "Http", "smb": "Smb", "websocket": "Websocket", "discord": "Discord", "github": "GitHub", "zoom": "Zoom"}

            def __init__(self):
                self.profiles = []
                self.crypto = []

            def addProfile(self, path, profile):
                self.profiles.append((path, profile))

            def addCrypto(self, path, crypto):
                self.crypto.append((path, crypto))

        builder = Builder()
        for profile_name, project_name in builder.PROFILE_PROJECTS.items():
            with self.subTest(profile=profile_name):
                namespace["buildProfile"](builder, BuildPath(), C2(), profile_name)
                self.assertEqual(project_name, builder.profiles[-1][1])
                self.assertEqual(("profile", BuildPath.name, profile_name, {"value": "forwarded"}), calls[-1])
        with self.assertRaises(ValueError):
            namespace["buildProfile"](builder, BuildPath(), C2(), "unsupported")

        namespace["buildConfig"](builder, BuildPath(), C2())
        self.assertEqual(("agent", BuildPath.name, "payload-uuid", {"value": "forwarded"}), calls[-1])
        self.assertEqual(BuildPath.name, builder.crypto[-1][0].name)
        self.assertEqual("Aes", builder.crypto[-1][1])

    def test_builder_registers_exact_supported_profile_maps(self):
        tree = ast.parse(GENERATOR_PATH.with_name("builder.py").read_text())
        builder_class = next(node for node in tree.body if isinstance(node, ast.ClassDef) and node.name == "athena")
        assignments = {
            node.targets[0].id: ast.literal_eval(node.value)
            for node in builder_class.body
            if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name)
            and node.targets[0].id in {"c2_profiles", "PROFILE_ROOTS", "PROFILE_PROJECTS"}
        }
        supported = ["http", "websocket", "smb", "discord", "github", "zoom"]
        self.assertEqual(supported, assignments["c2_profiles"])
        self.assertEqual(
            {
                "http": "Agent.Profiles.HTTP", "smb": "Agent.Profiles.SMB",
                "websocket": "Agent.Profiles.Websocket", "discord": "Agent.Profiles.Discord",
                "github": "Agent.Profiles.GitHub", "zoom": "Agent.Profiles.Zoom",
            },
            assignments["PROFILE_ROOTS"],
        )
        self.assertEqual(
            {"http": "Http", "smb": "Smb", "websocket": "Websocket", "discord": "Discord", "github": "GitHub", "zoom": "Zoom"},
            assignments["PROFILE_PROJECTS"],
        )
