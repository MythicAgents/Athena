+++
title = "vss-enum"
chapter = false
weight = 10
hidden = false
+++

## Summary
@TrustedSecs's implementation of [vss_enum](https://github.com/trustedsec/CS-Situational-Awareness-BOF) ported to Athena

Enumerate volume snapshots on a machine or share

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @TrustedSec  

### Arguments

#### hostname

- Description: Name of the machine account to enumerate
- Required Value: True  
- Default Value: None  

#### sharename

- Description: Name of the share to enumerate volume snapshots on
- Required Value: False  
- Default Value: C$  

## Usage

```
vss-enum -hostname myHost [-sharename myShare]
```

## MITRE ATT&CK Mapping

## Detailed Summary
