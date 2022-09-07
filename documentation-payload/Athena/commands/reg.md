+++
title = "reg"
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

- Description: The Action to perform with the plugin. [query, add, delete]
- Required Value: True  
- Default Value: 

#### hostname

- Description: The IP or Hostname to connect to
- Required Value: False  
- Default Value: 

#### keypath

- Description: The path to the registry values you want to query
- Required Value: True  
- Default Value: 

#### file

- Description: The name of the subkey to add
- Required Value: True  
- Default Value: 

#### file

- Description: The value of the registry key you want to add
- Required Value: True  
- Default Value: 

## Usage

```
reg query [keypath]

Recommended to use the modal for other actions
```


## Detailed Summary
