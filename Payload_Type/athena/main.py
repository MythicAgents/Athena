import mythic_container
from athena.mythic import *
import subprocess

#Kick off an initial load and obfuscation of the task plugins
p = subprocess.Popen(["dotnet", "build","AgentPlugins.sln", "/p:PluginsOnly=True", "-c Release", "--verbosity=q", "--nologo"], cwd="/Mythic/athena/agent_code/")
mythic_container.mythic_service.start_and_run_forever()