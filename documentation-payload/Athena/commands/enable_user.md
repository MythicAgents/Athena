+++
title = "enable-user"
chapter = false
weight = 10
hidden = false
+++

## Summary
@TrustedSecs's implementation of [enable_user](https://github.com/trustedsec/CS-Remote-OPs-BOF) ported to Athena

Perofrm a WMI query

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @TrustedSec  

### Arguments


#### username

- Description: The user to enable
- Required Value: True  
- Default Value:  

#### domain

- Description: The domain for the account
- Required Value: True  
- Default Value: 

## Usage

```
Command: enable-user
Summary: Activates (and if necessary enables) the specified user account on the target computer. 
Usage:   enable-user -username checkymander [-domain METEOR]
         username  Required. The user name to activate/enable. 
         domain    Optional. The domain/computer for the account. You must give 
                   the domain name for the user if it is a domain account.
```

## MITRE ATT&CK Mapping

## Detailed Summary
