+++
title = "patchit"
chapter = false
weight = 10
hidden = false
+++

## Summary
@ScriptIdiot's implementation of [BOF-Patchit](https://github.com/ScriptIdiot/BOF-patchit) ported to Athena

An all-in-one BOF to patch, check and revert AMSI and ETW for x64 process.

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @ScriptIdiot  

### Arguments

#### action

- Description: The action to perform
- Required Value: False  
- Default Value: None  

#### timeout

- Description: The timeout to wait for the arp scan to finish 
- Required Value: False  
- Default Value: 60  
## Usage
```
All-in-one to patch, check and revert AMSI and ETW for x64 process
Available Commands:" .
Check if AMSI & ETW are patched:      patchit check
Patch AMSI and ETW:                   patchit all
Patch AMSI (AmsiScanBuffer):          patchit amsi
Patch ETW (EtwEventWrite):            patchit etw
Revert patched AMSI & ETW:            patchit revertAll
Revert patched AMSI:                  patchit revertAmsi
Revert patched ETW:                   patchit revertEtw
Note: check command only compares first 4 lines of addresses of functions
```

## MITRE ATT&CK Mapping

## Detailed Summary
