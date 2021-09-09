+++
title = "jobkill"
chapter = false
weight = 10
hidden = false
+++

## Summary
Kill a job with the specified ID - not all jobs are killable. 

- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments
#### task-id

- Description: The guid of the job to kill
- Required Value: True  
- Default Value: None  
## Usage

```
jobkill [task-id]
```


## Detailed Summary

Kill a running job. Job ID's can be enumerated by running the `jobs` command