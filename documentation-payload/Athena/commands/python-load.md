+++
title = "python-load"
chapter = false
weight = 10
hidden = false
+++

## Summary
Load required Python libraries into the IronPython interpreter

- Needs Admin: False
- Version: 1
- Author: @checkymander

### Arguments
#### file

- Description: A zip file containing the libraries to import
- Required Value: False
- Default Value: None

## Usage

```
python-load
```

## Detailed Summary
Loads a zip archive containing Python libraries into the IronPython interpreter. Use this to load the standard library or third-party dependencies before running scripts with python-exec.
