+++
title = "exec"
chapter = false
weight = 10
hidden = false
+++

## Summary
Executes a command with the current default shell of the user.
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments
#### cmdline

- Description: The cmdline to execute
- Required Value: True  
- Default Value: None  

#### spoofedcmdline

- Description: Perform argument spoofing for the process
- Required Value: False  
- Default Value: None  
#### parent

- Description: Set the parent process ID for the spawned processd 
- Required Value: False  
- Default Value: None  

#### output

- Description: Get output from spawned process
- Required Value: False  
- Default Value: None  

#### suspended

- Description: Start process suspended
- Required Value: False  
- Default Value: None  



## Usage

```
exec -ppid=1234 -cmdline="net user checkymander" -spoofedcmdline="net user notcheckymander" -output=true -suspended=false
```

## MITRE ATT&CK Mapping

- T1059  
## Detailed Summary

Execute a shell command