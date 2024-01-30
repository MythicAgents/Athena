+++
title = "shellcode-inject"
chapter = false
weight = 10
hidden = false
+++

## Summary
Execute a shellcode buffer in a sacrificial process
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander   

### Arguments

#### file

- Description: The shellcode file to execute
- Required Value: True  
- Default Value: n/a

#### parent

- Description: Spoof parent PID 
- Required Value: False  
- Default Value:  n/a

#### spoofedcommandline

- Description: Command line arguments to spoof 
- Required Value: False  
- Default Value:  n/a

#### output

- Description: Display assembly output
- Required Value: True  
- Default Value:  n/a

#### commandline

- Description: The name of the process to inject into
- Required Value: True  
- Default Value:  n/a

## Usage
The shellcode-inject command is recommended to be configured via the modal which can be accessed by typing the following and pressing enter

```
inject-shellcode
inject-shellcode -spoofedcommandline <spoofedArgs> -output <True/false> -commandline <process to inject into> -parent <PID> -file <FILE>
```


## Detailed Summary
