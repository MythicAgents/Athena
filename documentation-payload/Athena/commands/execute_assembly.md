+++
title = "execute-assembly"
chapter = false
weight = 10
hidden = false
+++

## Summary
Exit the current session and kill the agent.

  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments
#### Assembly

- Description: The assembly to load into memory
- Required Value: True  
- Default Value: None  

#### Arguments

- Description: The arguments to pass to the assembly
- Required Value: False  
- Default Value: None  

## Usage

```
exit
```


## Detailed Summary
There are two things to note when running the execute-assembly command.



1. Once the assembly is executed, there is currently no way to stop it outside of exiting the agent. This will only be affected assemblies that continuously run in a loop.


2. Due to the way stdout is handled in .net core, only one assembly can be executed at a time. The agent will wait until the currently running assembly is executed before allowing you to run another.
