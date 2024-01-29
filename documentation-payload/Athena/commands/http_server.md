+++
title = "http-server"
chapter = false
weight = 10
hidden = false
+++

## Summary
Starts an HTTP Server on the host
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments
#### action

- Description: The action to be performed (start list remove)
- Required Value: True  
- Default Value: None  
#### port

- Description: The port to listen on
- Required Value: True  
- Default Value: None  
#### fileName

- Description: The name of the file being hosted by the server  
- Required Value: True  
- Default Value: None  
#### file

- Description: The file to be hosted
- Required Value: True  
- Default Value: None  

## Usage

```
http-server start -port 443
```

## Detailed Summary
