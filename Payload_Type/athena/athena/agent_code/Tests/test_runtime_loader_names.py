from pathlib import Path
from unittest import TestCase


ROOT = Path(__file__).parents[1]


class RuntimeAssemblyNameTests(TestCase):
    def test_command_loader_uses_build_agent_uuid_candidates(self):
        source = (ROOT / "Agent.Managers.Reflection" / "AssemblyManager.cs").read_text()
        self.assertIn("AssemblyIdentity.GetLoadCandidates", source)
        self.assertIn("agentConfig.build_uuid", source)

    def test_profile_loader_uses_build_agent_uuid_candidates(self):
        source = (ROOT / "AthenaCore" / "Config" / "ContainerBuilder.cs").read_text()
        self.assertIn("AssemblyIdentity.GetLoadCandidates", source)
        self.assertIn("build_uuid", source)
