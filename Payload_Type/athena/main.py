import mythic_container
from athena.mythic import *
import subprocess

agent_code = "/Mythic/athena/agent_code"

# Build the obfuscator tool once on container start
subprocess.run(
    ["dotnet", "build", "-c", "Release", f"{agent_code}/Obfuscator"],
    check=True,
)

# Build plugin projects that copy library DLLs into bin/
for proj in ["sftp", "ssh", "screenshot", "ds", "wmi"]:
    subprocess.run(
        ["dotnet", "build", "-c", "Release", f"{agent_code}/{proj}"],
        check=True,
    )

mythic_container.mythic_service.start_and_run_forever()
