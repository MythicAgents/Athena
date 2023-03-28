+++
title = "arp"
chapter = false
weight = 10
hidden = false
+++

## Summary
Perform an ARP scan n your local network.

- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments

#### cidr

- Description: The CIDR to scan 
- Required Value: True  
- Default Value: None  

#### timeout

- Description: The timeout to wait for the arp scan to finish 
- Required Value: False  
- Default Value: 60  
## Usage

```
arp -cdr <cidr> [-timeout=60]
```

## MITRE ATT&CK Mapping

## Detailed Summary
