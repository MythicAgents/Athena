import importlib.util
import json
import os
import shutil
import subprocess
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest import TestCase, skipUnless

AGENT_CODE = Path(__file__).parents[1]
GENERATOR_PATH = AGENT_CODE / ".." / "mythic" / "agent_functions" / "config_generator.py"


def usable_dotnet_environment():
    if shutil.which("dotnet") is None:
        return None
    environment = os.environ.copy()
    for candidate in (
        environment,
        {**environment, "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT": "1"},
    ):
        try:
            result = subprocess.run(
                ["dotnet", "--version"],
                check=False,
                capture_output=True,
                env=candidate,
                timeout=10,
            )
        except (OSError, subprocess.TimeoutExpired):
            continue
        if result.returncode == 0:
            return candidate
    return None


DOTNET_ENVIRONMENT = usable_dotnet_environment()


def load_generator():
    spec = importlib.util.spec_from_file_location("repo_config_generator", GENERATOR_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class GeneratedPayloadConfigTests(TestCase):
    @skipUnless(DOTNET_ENVIRONMENT is not None, "dotnet is unavailable")
    def test_generated_profile_config_is_consumed_by_typed_runtime(self):
        generator = load_generator()
        with TemporaryDirectory() as directory:
            project = Path(directory)
            generated_root = project / "generated"
            (generated_root / "Agent.Profiles.Http").mkdir(parents=True)
            generator.write_profile_config(
                generated_root,
                "http",
                {
                    "callback_host": "https://example.test",
                    "callback_port": "443",
                    "get_uri": "index",
                    "post_uri": "submit",
                    "query_path_name": "q",
                    "proxy_host": "",
                    "proxy_port": "",
                    "proxy_user": "",
                    "proxy_pass": "",
                    "headers": {"User-Agent": "Athena"},
                    "encrypted_exchange_check": "T",
                },
                xor_key=0x6B,
            )
            source = AGENT_CODE / "Agent.Profiles.Http" / "HttpChannelOptions.cs"
            (project / source.name).write_text(source.read_text())
            (project / "ConfigSmoke.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>'
                '<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>'
                '</PropertyGroup></Project>'
            )
            (project / "Program.cs").write_text(
                "using System.Text.Json; using Agent.Profiles; "
                "var options = JsonSerializer.Deserialize(ChannelConfig.Decode(), "
                "HttpChannelOptionsJsonContext.Default.HttpChannelOptions)!; "
                "Console.Write(JsonSerializer.Serialize(options, "
                "HttpChannelOptionsJsonContext.Default.HttpChannelOptions));"
            )
            result = subprocess.run(
                ["dotnet", "run", "--project", str(project / "ConfigSmoke.csproj"), "-c", "Release"],
                check=False,
                capture_output=True,
                text=True,
                env=DOTNET_ENVIRONMENT,
            )
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            actual = json.loads(result.stdout)
            self.assertEqual("https://example.test", actual["callback_host"])
            self.assertEqual(443, actual["callback_port"])
            self.assertTrue(actual["encrypted_exchange_check"])

    @skipUnless(DOTNET_ENVIRONMENT is not None, "dotnet is unavailable")
    def test_generated_agent_config_is_consumed_by_runtime(self):
        generator = load_generator()
        with TemporaryDirectory() as directory:
            project = Path(directory)
            generated_root = project / "generated"
            (generated_root / "AthenaCore" / "Config").mkdir(parents=True)
            output, crypto = generator.write_agent_config(
                generated_root,
                "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
                {
                    "AESPSK": {"enc_key": "test-key"},
                    "callback_interval": "15",
                    "callback_jitter": 25,
                    "killdate": "2030-01-02",
                },
                xor_key=0x37,
            )
            self.assertTrue(output.exists())
            self.assertEqual("Aes", crypto)
            generated = generator.normalize_agent_config(
                "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
                {"obfuscate": True},
            )[0]
            self.assertTrue(generated["plugin_contract_fingerprint_required"])
            for source in (
                AGENT_CODE / "AthenaCore" / "Config" / "AgentConfig.cs",
                AGENT_CODE / "AthenaCore" / "Config" / "AgentConfigOptions.cs",
                AGENT_CODE / "Agent.Models" / "Interfaces" / "IAgentConfig.cs",
            ):
                (project / source.name).write_text(source.read_text())
            (project / "ConfigSmoke.csproj").write_text(
                '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>'
                '<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>'
                '</PropertyGroup></Project>'
            )
            (project / "Program.cs").write_text(
                'using Agent.Config; using System.Text.Json; var c = new AgentConfig(); '
                'Console.Write(JsonSerializer.Serialize(new { c.uuid, c.psk, c.sleep, c.jitter }));'
            )
            result = subprocess.run(
                ["dotnet", "run", "--project", str(project / "ConfigSmoke.csproj"), "-c", "Release"],
                check=False,
                capture_output=True,
                text=True,
                env=DOTNET_ENVIRONMENT,
            )
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertEqual(
                {
                    "uuid": "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
                    "psk": "test-key",
                    "sleep": 15,
                    "jitter": 25,
                },
                json.loads(result.stdout),
            )


if __name__ == "__main__":
    import unittest

    unittest.main()
