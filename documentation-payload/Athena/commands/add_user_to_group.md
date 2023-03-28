+++
title = "add-user-to-group"
chapter = false
weight = 10
hidden = false
+++

## Summary
@TrustedSecs's implementation of [add_user_to_group](https://github.com/trustedsec/CS-Remote-OPs-BOF) ported to Athena

Perofrm a WMI query

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @TrustedSec  

### Arguments

#### hostname

- Description: The target computer to perform the addition on
- Required Value: False  
- Default Value: localhost  

#### username

- Description: The user to add to the specified group
- Required Value: True  
- Default Value:  

#### groupname

- Description: The group to add the user to
- Required Value: False  
- Default Value: 

#### domain

- Description: The domain/computer for the account. You must give the domain name for the user if it is a domain account
- Required Value: False  
- Default Value: 

## Usage

```
Add the specified user to the group. Domain groups only!

Usage:   add-user-to-group -username checkymander -groupname "Domain Admins" [-hostname GAIA-DC] [-domain METEOR]
         username   Required. The user name to activate/enable. 
         groupname  Required. The group to add the user to.
         hostname   Optional. The target computer to perform the addition on.
         domain     Optional. The domain/computer for the account. You must give 
                    the domain name for the user if it is a domain account.
```

## MITRE ATT&CK Mapping

## Detailed Summary
