+++
title = "kerberoast"
chapter = false
weight = 10
hidden = false
+++

## Summary
@Outflank's implementation of [Keberoast](https://github.com/outflanknl/C2-Tool-Collection/) ported to Athena

Perform a kerberoast against all available accounts in a domain or specified account

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @Outflank  

### Arguments

#### action

- Description: The action to perform
- Required Value: True  
- Default Value: None  

#### user

- Description: The user to kerberoast
- Required Value: False  
- Default Value: None

## Usage

```
List SPN enabled accounts:
    kerberoast list

List SPN enabled accounts without AES Encryption:
    kerberoast list-no-aes

Roast all SPN enabled accounts:
    kerberoast roast

Roast all SPN enabled accounts without AES Encryption:
    kerberoast roast-no-aes

Roast a specific SPN enabled account:
    kerberoast roast <username>
```

## MITRE ATT&CK Mapping

## Detailed Summary
