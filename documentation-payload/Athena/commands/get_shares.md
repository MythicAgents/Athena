+++
title = "get-shares"
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

## Usage

```
get-shares DC1.gaia.local,FS1.gaia.local,gaia.local
```


## Detailed Summary
