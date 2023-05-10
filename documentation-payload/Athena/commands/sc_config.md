+++
title = "sc-config"
chapter = false
weight = 10
hidden = false
+++

## Summary
@TrustedSecs's implementation of [sc_config](https://github.com/trustedsec/CS-Remote-OPs-BOF) ported to Athena

Configure a specified service against a host

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @TrustedSec  

### Arguments


#### servicename

- Description: The name of the service to create.
- Required Value: True 
- Default Value:  

#### binpath

- Description: The binary path for the service to execute
- Required Value: True  
- Default Value: 

#### errormode

- Description: The error mode of the service. (0 = ignore errors, 1 = normal errors, 2 = severe errors, 3 = critical errors)
- Required Value: True
- Default Value: 

#### startmode

- Description: The start mode for the service. (2 = auto start, 3 = demand start, 4 = disabled)"
- Required Value: True
- Default Value:

#### hostname

- Description: The target system
- Required Value: False
- Default Value: localhost

## Usage

```
sc-config -servicename myService -binpath C:\\Users\\checkymander\\Desktop\\malware.exe -errormode 0 -startmode 2 -hostname GAIA-DC
servicename      Required. The name of the service to create.
binpath      Required. The binary path of the service to execute.
errormode    Required. The error mode of the service. The valid 
            options are:
            0 - ignore errors
            1 - nomral logging
            2 - log severe errors
            3 - log critical errors
startmode    Required. The start mode for the service. The valid
            options are:
            2 - auto start
            3 - on demand start
            4 - disabled
hostname     Optional. The host to connect to and run the commnad on. The
            local system is targeted if a HOSTNAME is not specified.
```

## MITRE ATT&CK Mapping

## Detailed Summary
