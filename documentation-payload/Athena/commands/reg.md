+++
title = "reg"
chapter = false
weight = 10
hidden = false
+++

## Summary
Provides the ability to interact with the windows registry, this includes creation, modification and deletion.
  
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

#### keyPath

- Description: The path to the registry values you want to query
- Required Value: True  
- Default Value: 

#### keyName

- Description: The name of the subkey to add
- Required Value: True  
- Default Value: 

#### keyValue

- Description: The value of the registry key you want to add
- Required Value: True  
- Default Value: 


#### keyType

- Description: The type of key to add (string, dword, qword, binary, multi_string, expand_string) 
- Required Value: True  
- Default Value: 

## Usage

```
reg query [keypath]

Recommended to use the modal for other actions

If setting keyType of binary, the keyValue will need to be base64 encoded
```


## Detailed Summary
