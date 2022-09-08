+++
title = "sftp"
chapter = false
weight = 10
hidden = false
+++

## Summary
Connect to a host and perform actions using SFTP

- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments

#### Action
- Descripton: The action to perform against the server
- Supported Values: download, ls, cd, connect, disconnect, list-sessions, switch-session
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

- Description: Args to pass to the submodule when not using `connect` as the action. Check `usage` for more detail.
- Required: True
- ParameterGroup: Default

## Usage

Initiating a connection
```
sftp connect -username <user> -hostname <host/ip> [-password <password>] [-keypath </path/to/key>]
```
Note: Active Session will update to the new session anytime a connection is initiated

Displaying the contents of a file:
```
sftp download <file name or path to file>
```

Performing a directory listing:
```
sftp ls [/path/to/directory]
```

Note: this output will appear in the file browser

Change working directory:
```
sftp cd /path/to/directory
```

List active SFTP sessions:
```
sftp list-sessions
```

Switch active SFTP session:
```
sftp switch <session guid>
```

Disconnect active SFTP session:
```
sftp disconnect
```

## Required Dependencies
`Renci.SSHNet.dll`

Both libraries can be loaded using the command `load-module ssh`


## MITRE ATT&CK Mapping

## Detailed Summary
