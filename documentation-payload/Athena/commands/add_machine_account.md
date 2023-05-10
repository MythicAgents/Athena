+++
title = "add-machine-account"
chapter = false
weight = 10
hidden = false
+++

## Summary
@Outflank's implementation of [AddMachineACcount](https://github.com/outflanknl/C2-Tool-Collection/) ported to Athena

Create a machine account in the domain

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @Outflank  

### Arguments

#### computername

- Description: Name of the machine account to add
- Required Value: False  
- Default Value: None  

#### password

- Description: Password of the machine account to add
- Required Value: False  
- Default Value: None

## Usage

```
add-machine-account -computername MyComputer [-password P@ssw0rd]
```

## MITRE ATT&CK Mapping

## Detailed Summary
