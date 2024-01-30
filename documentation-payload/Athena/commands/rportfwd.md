+++
title = "rportfwd"
chapter = false
weight = 10
hidden = false
+++

## Summary
Display the contents of the user clipboard
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments

#### action

- Description: The action (start, stop)
- Required Value: True  
- Default Value: 

#### lport

- Description: The port to listen on the agent on
- Required Value: False  
- Default Value: 

#### rport

- Description: The port of the host to forward traffic back to
- Required Value: True  
- Default Value: 

#### rhost

- Description: The name of the host to forward traffic back to
- Required Value: True  
- Default Value: 

## Usage

```
rportfwd -action start -rhost 127.0.0.1 -rport 1234 -lport 1234
```


## Detailed Summary
