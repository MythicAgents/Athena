+++
title = "set-user-pass"
chapter = false
weight = 10
hidden = false
+++

## Summary
@TrustedSecs's implementation of [set_user_pass](https://github.com/trustedsec/CS-Remote-OPs-BOF) ported to Athena

Sets a users password on a domain

- Needs Admin: False  
- Version: 1  
- Author: @checkymander, @TrustedSec  

### Arguments

#### username

- Description: TTHe username to set the password for
- Required Value: True 
- Default Value:  

#### password

- Description: The password for the account, it must meet GPO requirements
- Required Value: True
- Default Value: localhost

#### domain

- Description: The domain name for the user if it's a domain account
- Required Value: True
- Default Value: localhost

## Usage

```
set-user-pass -username checkymander -password P@ssw0rd! -domain METEOR
         username  Required. The user name to activate/enable. 
         password  Required. The new password. The password must meet GPO 
                   requirements.
         domain    Required. The domain/computer for the account. You must give 
                   the domain name for the user if it is a domain account.
```

## MITRE ATT&CK Mapping

## Detailed Summary
