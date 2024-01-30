+++
title = "port-bender"
chapter = false
weight = 10
hidden = false
+++

## Summary
Redirect traffic arriving on one port to another port 
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments
#### port

- Description: The port to listen for traffic on
- Required Value: True  
- Default Value: None  
#### destination

- Description: The destination to redirect traffic to <host:port>
- Required Value: True  
- Default Value: None  

## Usage

```
port-bender -port 445 -destination 8445
```

## Detailed Summary
