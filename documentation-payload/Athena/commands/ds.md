+++
title = "dsquery"
chapter = false
weight = 10
hidden = false
+++

## Summary
Perform an LDAP query against a specified domain/server

- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments

#### Action
- Descripton: The action to perform against the server
- Supported Values: connect, query, disconnect
- Required: True
- ParameterGroup: Connect, Default

#### Action
- Descripton: The action to perform against the server
- Supported Values: connect, query, disconnect
- Required: False
- ParameterGroup: Connect, Default

#### Username

- Description: The username to authenticate to the host with
- Required: False
- ParameterGroup: Connect

#### Password

- Description: The password of the uer account or passphrase for the keyfile
- Required: False
- ParameterGroup: Connect

#### Domain

- Description: The Domain to authenticate against
- Required: False
- ParameterGroup: Connect

#### Server

- Description: The server to Bind to
- Required: False
- ParameterGroup: Connect, Default

#### LdapFilter

- Description: The custom query to perform against the server
- Required: False
- ParameterGroup: Default

#### ObjectCategory

- Description: Limit results to a specific object category
- Required: True
- Supported Values: *, user, group, ou, computer
- ParameterGroup: Default

#### SearchBase

- Description: The SearchBase to limit your query to
- Required: False
- ParameterGroup: Default

#### Properties

- Description: A comma separated string identifying the properties you want to return or `all` 
- Required: False
- ParameterGroup: Default


## Usage

```
ds-query -ldapfilter="(myfilter=*)" -searchbase="dc=example,dc=com" -objectCategory=* -properties=all

Initiate a bind using current context
ds-connect [-server <server>] [-domain <domain>] [-username <username>] [-password <password>]

```

## MITRE ATT&CK Mapping

## Detailed Summary

## Required Dependencies
`System.DirectoryServices.Protocols.dll`

Can be loaded using the `load-module domain` command
