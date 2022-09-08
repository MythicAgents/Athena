+++
title = "test-port"
chapter = false
weight = 10
hidden = false
+++

## Summary
Perform an NetShareEnum on the provided hosts (Windows only)
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments

#### hosts

- Description: Comma separated list of hosts 
- Required Value: True  
- Default Value: 

#### inputlist

- Description: List of hosts in a newline separated file
- Required Value: True  
- Default Value: 


#### ports

- Description: TCP ports to check (comma separated)
- Required Value: True  
- Default Value: 

## Usage

```
test-port DC1.gaia.local,FS1.gaia.local,gaia.local 80,443,8080,8000,8443
```


## Detailed Summary
