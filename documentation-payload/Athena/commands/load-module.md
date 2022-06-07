+++
title = "load-module"
chapter = false
weight = 10
hidden = false
+++

## Summary
Wrapper command to load required assemblies for certain executables

- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments

#### module

- Description: The module to load
- Supported Values: ssh, domain
- Required: True

## Usage

```
load-module <module>
```

## MITRE ATT&CK Mapping

## Detailed Summary
The `ssh` submodule loads the following DLLs:
`Renci.SSHNet.dll`
`SshNet.Security.Cryptography.dll`

The `domain` submodule loads the following DLL:
`System.DirectoryServices.Protocols.dll`
