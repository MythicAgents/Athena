+++
title = "execute-module"
chapter = false
weight = 10
hidden = false
+++

## Summary
Load a supported Athena module in memory and execute it

- Needs Admin: False
- Version: 1
- Author: @checkymander

### Arguments
#### file

- Description: The module file to load and execute
- Required Value: True
- Default Value: None

#### name

- Description: The name of the module
- Required Value: True
- Default Value: None

#### entrypoint

- Description: The entrypoint function to call
- Required Value: True
- Default Value: Execute

#### arguments

- Description: Arguments to pass to the module
- Required Value: True
- Default Value: None

## Usage

```
execute-module
```

## Detailed Summary
Loads a supported Athena module assembly into memory and invokes the specified entrypoint function.
