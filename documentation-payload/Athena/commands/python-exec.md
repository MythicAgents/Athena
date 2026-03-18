+++
title = "python-exec"
chapter = false
weight = 10
hidden = false
+++

## Summary
Execute a Python file using the IronPython3 interpreter. Use python-load to add required dependencies.

- Needs Admin: False
- Version: 1
- Author: @checkymander

### Arguments
#### pyfile

- Description: Python file to execute
- Required Value: False
- Default Value: None

#### args

- Description: Arguments to pass to the script via argv
- Required Value: False
- Default Value: None

## Usage

```
python-exec
```

## Detailed Summary
Executes a Python script in-process using IronPython3. Use python-load to import the standard library or other dependencies before executing scripts that require them.
