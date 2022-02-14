#!/bin/bash

cd /Mythic/agent_code/AthenaPlugins
dotnet publish -c release

cd /Mythic/mythic
cp ~/.nuget/packages/system.directoryservices.protocols/6.0.0/runtimes/win/lib/net6.0/System.DirectoryServices.Protocols.dll /Mythic/agent_code/AthenaPlugins/bin/windows/
cp ~/.nuget/packages/system.directoryservices.protocols/6.0.0/runtimes/osx/lib/net6.0/System.DirectoryServices.Protocols.dll /Mythic/agent_code/AthenaPlugins/bin/macos/
cp ~/.nuget/packages/system.directoryservices.protocols/6.0.0/runtimes/linux/lib/net6.0/System.DirectoryServices.Protocols.dll /Mythic/agent_code/AthenaPlugins/bin/linux/

export PYTHONPATH=/Mythic:/Mythic/mythic

python3.8 mythic_service.py
