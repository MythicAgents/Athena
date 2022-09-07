
<p align="center">
  <img src="agent_icons/athena.svg">
</p>

# Athena
Athena is a fully-featured cross-platform agent designed using the .NET 6. Athena is designed for Mythic 2.2 and newer. The current recommended version is 2.3+ however, 2.2 will still function, although task output won't be as nice. As this is still an early release, bugs are expected.

## Features
- Crossplatform
  - Windows
  - Linux
  - OSX
  - Potentially More!
- SOCKS5 Support (Beta)
- P2P Agent support
	- SMB
	- More coming soon
- Reflective loading of Assemblies
- Modular loading of commands
- Easy plugin development
- Easy development of new communication methods

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
Athena can communicate over a slack channels.

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
- NativeAOT
  - Alternative Medium option. NativeAOT is still in development for the .NET framework. This allows the entire payload to be statically compiled, however you lose the ability to reflectively load plugins. So you'll be limited to built-in commands, SOCKS5, and SMB support.
  - File Size: 28MB

## Credit
[@its_a_feature_](https://twitter.com/its_a_feature_) - Creator of the Mythic framework

[@0okamiseishin](https://twitter.com/0okamiseishin) - For creating the Athena logo

[@djhohnstein](https://twitter.com/djhohnstein) - For crypto code, and advice regarding development

[@tr41nwr3ck](https://twitter.com/Tr41nwr3ck48) - For plugin development

## Changelog

XX/XX/XX - 0.2 release
	- Refactor of base agent code
	- Refactor of plugin loading capabilities
	- Improvements to SMB C2 Profile
	- Slack C2 Profile support
	- Stability Improvements
	- Support for `ps` and `ls` Mythic Hooks
	- Improvements to executable trimming to allow included plugins to be fully supported
  - Added Commands
    - kill

02/15/22 - 0.1 release of Athena
	- Initial Release


