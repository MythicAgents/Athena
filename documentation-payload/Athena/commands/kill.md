+++
title = "kill"
chapter = false
weight = 10
hidden = false
+++

## Summary
Kill a process by name or ID

If a name is specified, any instance of that process will be attempted to killed
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments
#### id

- Description: The ID of the process to kill
- Required Value: True  
- Default Value: None  
#### name

- Description: The name of the processes to kill
- Required Value: True  
- Default Value: None  

## Usage

```
kill -id 1234
kill -name chrome
```

## Detailed Summary
