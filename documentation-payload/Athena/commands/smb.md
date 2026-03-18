+++
title = "smb"
chapter = false
weight = 10
hidden = false
+++

## Summary
Initiate or manage a connection with an SMB-linked Athena agent

- Needs Admin: False
- Version: 1
- Author: @checkymander

### Arguments
#### action

- Description: Action to perform (link, unlink, list)
- Required Value: True
- Default Value: None

#### hostname

- Description: The hostname to connect to (required for link)
- Required Value: False
- Default Value: None

#### pipename

- Description: The named pipe the agent is listening on (required for link)
- Required Value: False
- Default Value: None

## Usage

```
smb -action link -hostname [host] -pipename [pipename]
smb -action unlink
smb -action list
```

## Detailed Summary
Links to an SMB peer-to-peer Athena agent over a named pipe, unlinking it, or listing currently linked agents.
