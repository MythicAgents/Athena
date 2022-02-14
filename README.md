
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
- SMB Agent support (Beta)
- Reflective loading of .NET 6 Assemblies
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

### SMB
Athena supports SMB communications for internal comms over named pipes.

## Opsec Considerations
### Agent Size
There are multiple ways Athena can be built which have a large effect on the final size of the payload

- Standard
  - The smallest option. This contains just the base agent code, and can be executed on any machine where .NET 5 is already installed.
  - File Size: 67KB
- Self Contained
  - The largest option. This contains the base agent code, and the entire .NET 5 framework. This file will be very large, but will allow for the most compatibility and flexibility when operating. Compression shrinks this size down significantly.
  - File Size: 63MB
  - Compressed Size: 33MB
- Self-Contained Trimmed
  - Medium option. This contains the baes agent code, and only the required .NET 5 framework libraries. This file is smaller than the regular self contained option, however at least for now, you lose the ability to reflectively load plugins. So you're pretty much limited to built-in commands, SOCKS5, and SMB support.
  - File Size: 18MB
  - Compressed Size: 12.4MB
- NativeAOT
  - Alternative Medium option. NativeAOT is still in development for the .NET framework. This allows the entire payload to be statically compiled, however you lose the ability to reflectively load plugins. So you'll be limited to built-in commands, SOCKS5, and SMB support.
  - File Size: 33.5MB
  - Trim Size: 12.4MB

### AMSI
 - AMSI.dll does not seem to be loaded as part of .NET 6 executables. So no AMSI bypasses are needed.

## Credit
[@its_a_feature_](https://twitter.com/its_a_feature_) - Creator of the Mythic framework

[@0okamiseishin](https://twitter.com/0okamiseishin) - For creating the Athena logo

[@djhohnstein](https://twitter.com/djhohnstein) - For crypto code, and advice regarding development

[@tr41nwr3ck](https://twitter.com/Tr41nwr3ck48) - For plugin development

## Changelog
02/15/22 - 0.1 release of Athena


