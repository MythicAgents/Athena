+++
title = "co"
chapter = false
weight = 10
hidden = false
+++

## Summary
Athena implementation of the [RunOF Project](https://github.com/nettitude/RunOF) used to execute COFF/BOF files in the agent process. 

- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments

#### coffFile

- Description: The COFF file to execute in the agent process 
- Required Value: True  
- Default Value: None  

#### functionName

- Description: The function name in the BOF to execute
- Required Value: False  
- Default Value: go  

#### arguments

- Description: The generated arguments that are to be provided to the BOF
- Required Value: False  
- Default Value: n/a

#### timeout

- Description: The timeout to wait for the BOF to finish executing
- Required Value: False  
- Default Value: 60  

## Usage
The coff command is recommended to be configured via the modal which can be accessed by typing the following and pressing enter

```
coff
```

## MITRE ATT&CK Mapping

## Detailed Summary
