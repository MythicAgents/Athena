+++
title = "powershell-script"
chapter = false
weight = 10
hidden = false
+++

## Summary
Execute a powershell script.  
  
- Needs Admin: False  
- Version: 1  
- Author: @ascemama  

### Arguments
#### command

- Description: The script to execute
- Required Value: True  
- Default Value: None  
#### arguments

- Description: The arguments to pass to the script 
- Required Value: False  
- Default Value: None  

- Description: The additionnal command to run after the script 
- Required Value: False  
- Default Value: None  

## Usage

```
powershell-script [script file] [script argument] [additionnal commands]
```

## MITRE ATT&CK Mapping

- T1059  
## Detailed Summary

Execute a powershell script. Additionally it is possible to add a command which is run after the script.
Note that the same powershell runspace is used, meaning variables and function can be reused in later powershell script commands. Also some powershell .NET framework commands, do not exist in powershell .NET Core.  