+++
title = "ssh"
chapter = false
weight = 10
hidden = false
+++

## Summary
Spawns a pseudo interactive SSH prompt with a machine

- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments
#### hostname

- Description: The IP or Hostname of the SSH server to connect to
- Required: True
- ParameterGroup: Connect

#### Username

- Description: The username to authenticate to the host with
- Required: True
- ParameterGroup: Connect

#### Password

- Description: The password of the uer account or passphrase for the keyfile
- Required: False
- ParameterGroup: Connect

#### KeyPath

- Description: The path to the keyfile or an empty string if no keyfile is being used for authentication
- Required: False
- ParameterGroup: Connect

## Usage

Initiating a connection:
```
ssh -hostname <host/ip> -username <user> [-password <password>] [-keypath </path/to/key>]
```

## Required Dependencies
`Renci.SSHNet.dll`

## MITRE ATT&CK Mapping

## Detailed Summary
