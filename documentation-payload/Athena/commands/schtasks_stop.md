+++
title = "schtasks-stop"
chapter = false
weight = 10
hidden = false
+++

## Summary
@TrustedSecs's implementation of [schtasks_stop](https://github.com/trustedsec/CS-Remote-OPs-BOF) ported to Athena

Stops a specified scheduled task against a host

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @TrustedSec  

### Arguments

#### taskname

- Description: The name of the scheduled task to stop.
- Required Value: True 
- Default Value:  

#### hostname

- Description: The target system
- Required Value: False
- Default Value: localhost

## Usage

```
schtasks-stop -hostname GAIA-DC -taskname \\Microsoft\\Windows\\MUI\\LpRemove
         hostname  Optional. The target system (local system if not specified)
         taskname  Required. The scheduled task name.
Note:    The full path including the task name must be given, e.g.:
             schtasks-stop \\Microsoft\\Windows\\MUI\\LpRemove
             schtasks-stop \\Microsoft\\windows\\MUI\\totallyreal
```

## MITRE ATT&CK Mapping

## Detailed Summary
