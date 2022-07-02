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

Execute a powershell command. 
Note that the same powershell runspace is used, meaning variables and function can be reused in later commands. Also some powershell .NET framework commands do not exist in powershell .NET Core. 
  