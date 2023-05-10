+++
title = "inject-assembly"
chapter = false
weight = 10
hidden = false
+++

## Summary
Execute a PE/.NET Framework assembly in a sacrificial process, uses donut to convert the assembly to shellcode and injects it.
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander   

### Arguments

#### file

- Description: The assembly file to execute
- Required Value: True  
- Default Value: n/a

#### arch

- Description: Target architecture for loader: 

        1=x86 
        2=amd64
        3=x86+amd64
- Required Value: False  
- Default Value:  3

#### bypass

- Description: Behavior for bypassing AMSI/WLDP : 

        1 = None
        2 = Abort on fail
        3 = Continue on fails 
- Required Value: False  
- Default Value:  3

#### exit_opt

- Description: "Determines how the loader should exit.

        1 = exit thread, 
        2 = exit process
        3 = Do not exit or cleanup and block indefinitely
- Required Value: False  
- Default Value:  2

#### processName

- Description: The process name (or full path t0 the executable) to spawn and inject into
- Required Value: True  
- Default Value:  n/a

 #### arguments

- Description: Remove the file from every sub folder of the specified path 
- Required Value: False  
- Default Value:  

## Usage
The inject-assembly command is recommended to be configured via the modal which can be accessed by typing the following and pressing enter

```
inject-assembly
```

## Additional Information
This command cannot be loaded directly, to load the inject-assembly command be sure to `load shellcode-inject` and it will become available

## Detailed Summary
