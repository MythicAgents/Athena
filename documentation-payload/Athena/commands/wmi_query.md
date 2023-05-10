+++
title = "wmi-query"
chapter = false
weight = 10
hidden = false
+++

## Summary
@TrustedSecs's implementation of [wmi_query](https://github.com/trustedsec/CS-Situational-Awareness-BOF) ported to Athena

Perofrm a WMI query

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @TrustedSec  

### Arguments

#### hostname

- Description: Name of the machine to perform the query against
- Required Value: False  
- Default Value: localhost  

#### query

- Description: The query to perform
- Required Value: True  
- Default Value:  

#### namespace

- Description: Name of the share to enumerate volume snapshots on
- Required Value: False  
- Default Value: root\\cimv2

## Usage

```
Summary: This command runs a general WMI query on either a local or remote machine and displays the results in a comma separated table.
Usage:   wmi_query -query <query> -namespace <namespace> [-hostname <host>]
		 query		- The query to run. The query should be in WQL.
		 hostname	   - Optional. Specifies the remote system to connect to. Do
						not include or use '.' to indicate the command should
						be run on the local system.
		 namespace	- Optional. Specifies the namespace to connect to. This
						defaults to root\\cimv2 if not specified.
Note:	You must have a valid login token for the system specified if not local.
```

## MITRE ATT&CK Mapping

## Detailed Summary
