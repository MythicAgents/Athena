+++
title = "powershell-command"
chapter = false
weight = 10
hidden = false
+++

## Summary
Execute a powershell command. 
  
- Needs Admin: False  
- Version: 1  
- Author: @ascemama  

### Arguments
#### command

- Description: The command to execute
- Required Value: True  
- Default Value: None  


## Usage

```
powershell-command [command] [args]
```

## MITRE ATT&CK Mapping

- T1059  
## Detailed Summary

Execute a powershell command. Note that :
 - this plugin works only on windows
 - some powershell .NET framework commands do not exist in powershell .NET Core
 - the plugin does not work when payload is built as single file. Due to an open powershell SDK bug : PowerShell/PowerShell#13540
 - the plugin must be built for win10-x64 or win10-x86 target. which should work with windows10/11 and windows server versions. The target win-x86/64 does not work. 
 - the same powershell runspace is used, meaning variables and function can be reused in later commands.
  