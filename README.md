![Athena](agent_icons/athena.svg)

# Athena
Athena is a fully-featured cross-platform agent designed using the .NET 5. Athena is designed for Mythic 2.2 and newer, and will not work on older versions. Athena is currently under an alpha release, so expect some bugs initially.

## Features
- Crossplatform
  - Windows
  - Linux
  - OSX
  - Potentially More!
- SOCKS5 Support (Beta)
- SMB Agent support (Beta)
- Reflective loading of .NET 5 Assemblies
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

### SMBServer
Athena can act as an SMB server, facilitating communications between its egress channel and the SMBClient.

Note: Multiple agents can be chained to support advanced internal communications.

#### todo pictures

### SMBClient
Athena can communicate with the mythic server through an external egress agent by making use of named pipes. SMBClient agents can act as both a Client and a Server, allowing for chaining of multiple agents together.

## Opsec Considerations
### Agent Size
There are multiple ways Athena can be built which have a large effect on the final size of the payload

- Standard
  - The smallest option. This contains just the base agent code, and can be executed on any machine where .NET 5 is already installed.
  - File Size: 58kb
- Self Contained
  - The largest option. This contains the base agent code, and the entire .NET 5 framework. This file will be very large, but will allow for the most compatibility and flexibility when operating.
  - File Size: 60mb
  - OpSec Note: Until .NET 6 is released, the full .NET 5 framework will be unzipped to a temporary folder to execute Athena. This will be resolved once .NET 6 is fully released.
- Self-Contained Trimmed
  - Medium option. This contains the baes agent code, and only the required .NET 5 framework libraries. This file is smaller than the regular self contained option, however at least for now, you lose the ability to reflectively load plugins. So you're pretty much limited to built-in commands, SOCKS5, and SMB support.
  - OpSec Note: Until .NET 6 is released, the full .NET 5 framework will be unzipped to a temporary folder to execute Athena. This will be resolved once .NET 6 is fully released.
  - File Size: 23mb
- NativeAOT
  - Alternative Medium option. NativeAOT is still in development for the .NET framework. This allows the entire payload to be statically compiled, however you lose the ability to reflectively load plugins. So you'll be limited to built-in commands, SOCKS5, and SMB support.
  - File Size: 37mb

### AMSI
 - Athena does not come built-in with any AMSI bypass methods. This is left up to the operator to implement.

## Credit
[@its_a_feature_](https://twitter.com/its_a_feature_) - Creator of the Mythic framework

[@0okamiseishin](https://twitter.com/0okamiseishin) - For creating the Athena logo

[@djhohnstein](https://twitter.com/djhohnstein) - For crypto code, and advice regarding development

[@r41nwr3ck](https://twitter.com/Tr41nwr3ck48) - For plugin development

## Changelog
MM/DD/YY - 0.1 Alpha release of Athena


