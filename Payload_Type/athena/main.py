import mythic_container
from athena.mythic import *
import subprocess

# Build the obfuscator tool once on container start
obfuscator_dir = "/Mythic/athena/agent_code/Obfuscator"
subprocess.run(
    ["dotnet", "build", "-c", "Release", obfuscator_dir],
    check=True,
)

mythic_container.mythic_service.start_and_run_forever()
