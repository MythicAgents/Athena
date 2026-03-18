+++
title = "schtasks-create"
chapter = false
weight = 10
hidden = false
+++

## Summary
TrustedSec's implementation ported to Athena. Create a scheduled task on a local or remote host

- Needs Admin: False
- Version: 1
- Author: @TrustedSec

### Arguments
#### taskfile

- Description: The XML task definition file for the created task
- Required Value: True
- Default Value: None

#### taskpath

- Description: The full path for the created task (e.g. \MyTasks\MyTask)
- Required Value: True
- Default Value: None

#### usermode

- Description: The username association mode (user, xml, system)
- Required Value: True
- Default Value: None

#### forcemode

- Description: Creation disposition (create, update)
- Required Value: True
- Default Value: None

#### hostname

- Description: The target system
- Required Value: False
- Default Value: localhost

## Usage

```
schtasks-create -taskpath \MyTasks\MyTask -usermode system -forcemode create
```

## Detailed Summary
Creates a scheduled task on the local or a remote system using an XML task definition file.
