import mythic_container
from athena.mythic import *
import subprocess

#I just need it to do something
#Kick off an initial load and obfuscation of the task plugins
p = subprocess.Popen(["dotnet", "build", "AgentPlugins.sln", "/p:PluginsOnly=True", "/p:Obfuscate=True", "-c", "Release"], cwd="/Mythic/athena/agent_code/")
mythic_container.mythic_service.start_and_run_forever()