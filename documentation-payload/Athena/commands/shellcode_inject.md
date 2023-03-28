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

#### processName

- Description: The process name (or full path t0 the executable) to spawn and inject into
- Required Value: True  
- Default Value:  n/a


## Usage
The shellcode-inject command is recommended to be configured via the modal which can be accessed by typing the following and pressing enter

```
shellcode-inject
```


## Detailed Summary
