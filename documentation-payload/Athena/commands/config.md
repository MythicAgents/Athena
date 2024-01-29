+++
title = "config"
chapter = false
weight = 10
hidden = false
+++

## Summary
Display the contents of the user clipboard
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments

#### sleep

- Description: The sleep of the agent
- Required Value: True  
- Default Value: 

#### jitter

- Description: The jitter of the agents sleep
- Required Value: False  
- Default Value: 

#### inject

- Description: The inject technique to set (1,2,3)
- Required Value: True  
- Default Value: 

#### killDate

- Description: The killdate for the agent to exit
- Required Value: True  
- Default Value: 

#### chunk_size

- Description: The chunksize for file upload/download jobs
- Required Value: True  
- Default Value: 


#### prettyOutput

- Description: If true, display formatted output, if false display raw output
- Required Value: True  
- Default Value: 

#### debug

- Description: Return debug messages from tasks
- Required Value: True  
- Default Value: 

## Usage

```
config -sleep=10 -jitter=40 -prettyOutput=true -chunk_size=56000 -killdate=mm/dd/yyyy -inject=1
```

## Detailed Summary
