+++
title = "schtasks-delete"
chapter = false
weight = 10
hidden = false
+++

## Summary
TrustedSec's implementation ported to Athena. Delete a scheduled task or folder on a local or remote host

- Needs Admin: False
- Version: 1
- Author: @TrustedSec

### Arguments
#### taskname

- Description: The task or folder name to delete (full path)
- Required Value: True
- Default Value: None

#### tasktype

- Description: The type of target to delete (folder, task)
- Required Value: False
- Default Value: None

#### hostname

- Description: The target system
- Required Value: False
- Default Value: localhost

## Usage

```
schtasks-delete -taskname \MyTasks\MyTask
schtasks-delete -taskname \MyTasks -tasktype folder
```

## Detailed Summary
Deletes a scheduled task or task folder on the local or a remote system.
