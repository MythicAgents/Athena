+++
title = "nanorubeus"
chapter = false
weight = 10
hidden = false
+++

## Summary
@wavv's implementation of [nanorobeus](https://github.com/wavvs/nanorobeus/) ported to Athena

COFF file (BOF) for managing Kerberos tickets.

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @ScriptIdiot  

### Arguments

#### action

- Description: The action to perform
- Required Value: False  
- Default Value: None  

#### luid

- Description: The luid to perform the action against
- Required Value: False  
- Default Value:   

#### ticket

- Description: The ticket to be used for the action 
- Required Value: False  
- Default Value:   

#### all

- Description: When supported perform action on all possible objects
- Required Value: False  
- Default Value: False  

#### spn

- Description: The service principal name to roast
- Required Value: False  
- Default Value:   

## Usage

```
Usage: nanorubeus [command] [options]
luid - get current logon ID
sessions [-luid <0x0> | -all] - get logon sessions
klist [-luid <0x0> | -all] - list Kerberos tickets
dump [-luid <0x0> | -all] - dump Kerberos tickets
ptt -ticket <base64> [-luid <0x0>] - import Kerberos ticket into a logon session
purge [-luid <0x0>] - purge Kerberos tickets
tgtdeleg -spn <spn> - retrieve a usable TGT for the current user
kerberoast -spn <spn> - perform Kerberoasting against specified SPN"
```

## MITRE ATT&CK Mapping

## Detailed Summary
