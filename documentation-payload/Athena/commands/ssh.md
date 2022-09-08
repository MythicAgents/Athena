+++
title = "ssh"
chapter = false
weight = 10
hidden = false
+++

## Summary
Connect to a host and perform actions using SSH

- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments

#### Action

- Description: The action to perform against the server
- Supported Values: connect, disconnect, exec, list-sessions, switch-session
- Required: True
- ParameterGroup: Connect, Default

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

#### args

Args to pass to the submodule when not using `connect` as the action. Check `usage` for more detail.

## Usage

Initiating a connection:
```
ssh connect -username <user> -hostname <host/ip> [-password <password>] [-keypath </path/to/key>]
```
Note: Active Session will update to the new session anytime a connection is initiated

Executing a command:
```
ssh exec <command>
```


List active SFTP sessions:
```
ssh list-sessions
```

Switch active SFTP session:
```
ssh switch <session guid>
```

Disconnect active SFTP session:
```
ssh disconnect
```

## Required Dependencies
`Renci.SSHNet.dll`

Library can be loaded using the command `load-module ssh`

## MITRE ATT&CK Mapping

## Detailed Summary
