#!/bin/bash

cd /Mythic/agent_code/AthenaPlugins
#dotnet build -c release
dotnet publish -c release

cd /Mythic/mythic

export PYTHONPATH=/Mythic:/Mythic/mythic

python3.8 mythic_service.py
