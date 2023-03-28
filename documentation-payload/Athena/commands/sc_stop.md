+++
title = "sc-stop"
chapter = false
weight = 10
hidden = false
+++

## Summary
@TrustedSecs's implementation of [sc_stop](https://github.com/trustedsec/CS-Remote-OPs-BOF) ported to Athena

Stop a specified service against a host

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @TrustedSec  

### Arguments


#### servicename

- Description: The name of the service to stop.
- Required Value: True 
- Default Value:  

#### hostname

- Description: The target system
- Required Value: False
- Default Value: localhost

## Usage

```
sc-stop -servicename myService -hostname GAIA-DC
         sc-stop -servicename myService
         servicename  Required. The name of the service to stop.
         hostname Optional. The host to connect to and run the commnad on. The
                  local system is targeted if a hostname is not specified.
```

## MITRE ATT&CK Mapping

## Detailed Summary
