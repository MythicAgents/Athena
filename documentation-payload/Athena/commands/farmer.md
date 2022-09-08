+++
title = "farmer"
chapter = false
weight = 10
hidden = false
+++

## Summary
Farmer is a project for collecting NetNTLM hashes in a Windows domain

- Needs Admin: False  
- Version: 1  
- Author: @domchell, @checkymander  

### Arguments

#### port

- Description: The port to listen on  
- Required Value: True  
- Default Value: 

## Usage
```
Farmer https://github.com/mdsecactivebreach/Farmer
    created by @domchell

Farmer is acts as a WebDAV server in order to catch NetNTLMv2 Authentication hashes from Windows clients.

The server will listen on the specified port and will respond to any WebDAV request with a 401 Unauthorized response. The server will then wait for the client to send the NTLMv2 authentication hash.
Usage: farmer [port]
     
```
