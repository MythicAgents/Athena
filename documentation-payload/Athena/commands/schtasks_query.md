+++
title = "schtasks-query"
chapter = false
weight = 10
hidden = false
+++

## Summary
@TrustedSecs's implementation of [schtasks_query](https://github.com/trustedsec/CS-Situational-Awareness-BOF) ported to Athena

Query scheduled task on a specified host

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @TrustedSec  

### Arguments

#### hostname

- Description: Name of the machine account to enumerate
- Required Value: False  
- Default Value: None  

#### taskName

- Description: Name of the machine account to enumerate
- Required Value: False  
- Default Value: None  

## Usage

```
 schtasks-query -taskName \\Microsoft\\Windows\\MUI\\LpRemove [-hostname myHost]
Note the task name must be given by full path including taskname, ex. \\Microsoft\\Windows\\MUI\\LpRemove
```

## MITRE ATT&CK Mapping

## Detailed Summary
