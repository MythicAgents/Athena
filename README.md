[![Agent Builds](https://github.com/MythicAgents/Athena/actions/workflows/dotnet-desktop.yml/badge.svg?branch=main)](https://github.com/MythicAgents/Athena/actions/workflows/dotnet-desktop.yml)
[![Build and push container images](https://github.com/MythicAgents/Athena/actions/workflows/docker.yml/badge.svg?branch=main)](https://github.com/MythicAgents/Athena/actions/workflows/docker.yml)


<p align="center">
  <img src="agent_icons/athena_old.svg">
</p>

# Athena
Athena is a fully-featured cross-platform agent designed using the crossplatform version of .NET (not to be confused with .Net Framework). Athena is designed for Mythic 3.0 and newer.

## Features
- Crossplatform
  - Windows
  - Linux
  - OSX
  - Potentially More!
- SOCKS5 Support
- Reverse Port Forwarding
- P2P Agent support
	- SMB
	- More coming soon
- Reflective loading of Assemblies
- Modular loading of commands
- Easy plugin development
- Easy development of new communication methods
- BOF Support

## Installation

1.) Install Mythic from [here](https://github.com/its-a-feature/Mythic)

2.) From the Mythic install directory run the following command:

`./mythic-cli install github https://github.com/MythicAgents/Athena`

## Supported C2 Profiles

### HTTP
Athena can act as an egress channel over the default `http` Profile in use by Mythic. 

Note: All taskings and Responses are done via POST requests. So the GET URI parameter is unnecessary at this time.

### Websockets
Athena can act as an egress channel over the `websocket` profile. This is the recommended profile to use when making use of the SOCKS5 functionality.

### Slack
Athena can communicate over slack channels.

Note: Due to slack API rate limiting, the number of agents that can be executed at once using a specific workspace/token combination is limited. A lower sleeptime supports more agents.

### Discord
Athen can communicate over discord channels.

Note: Due to slack API rate limiting, the number of agents that can be executed at once using a specific workspace/token combination is limited. A lower sleeptime supports more agents.

### SMB
Athena supports SMB communications for internal comms over named pipes.

## Opsec Considerations
### Agent Size
There are multiple ways Athena can be built which have a large effect on the final size of the payload

- Standard
  - The smallest option. This contains just the base agent code, and requires you to package all of the DLLs with the agent. Not great for phishing, but the option is there if you want it.
  - File Size: 114KB
- Self Contained
  - The largest option. This contains the base agent code, and the entire .NET framework. This file will be very large, but will allow for the most flexibility when operating. Compression shrinks this size down dramatically
  - File Size: 63MB
  - Compressed Size: 33.8MB
- Self-Contained Trimmed
  - Medium option. This contains the base agent code, and only the required libraries. This file is smaller than the regular self contained option, however you may encounter some difficulties with custom `execute-assembly` assemblies. You will need to load their dependencies manually using `load-assembly` even if they're usually built into the framework
  - File Size: 18.5MB
  - Compressed Size: 12.8MB

## Credit
[@its_a_feature_](https://twitter.com/its_a_feature_) - Creator of the Mythic framework

[@0okamiseishin](https://twitter.com/0okamiseishin) - For creating the Athena logo

[@djhohnstein](https://twitter.com/djhohnstein) - For crypto code, and advice regarding development

[@tr41nwr3ck](https://twitter.com/Tr41nwr3ck48) - For plugin development & testing

## Known Issues
- Athena cannot be converted to shellcode
  - Due to the nature of self-contained .NET executables, Athena is currently unable to be converted to shellcode with tool such as donut
- Large Binary Sizes
  - Athena binaries default to being "self-contained", this essentially means the entire .NET runtime is included in the binary leading to larger sizes. If you need smaller binaries, experiment with the `trimmed`, and `compressed` options.
- Athena doesn't work with <insert common .NET executable here>
  - Athena is built using the latest version of .NET which is fundamentally different from the .NET Framework, which a majority of offensive security tools use. Any .NET Framework binaries will need to be converted to .NET 7 before they can be used with `execute-assembly` alternatively, you can use `inject-assembly` to use `donut` to convert it to shellcode and inject into a sacrificial process.
