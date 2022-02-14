+++
title = "load-assembly"
chapter = false
weight = 10
hidden = false
+++

## Summary
Load an assembly into the current AssemblyLoadContext
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments

#### Assembly

- Description: The DLL of the assembly to load
- Required Value: True  
- Default Value: None  

## Usage

```
load-assembly [assembly]
```

## Detailed Summary
`load-assembly` will allow you to make use of pluins that require 3rd party libraries before running execute-assembly. This makes execution easier, allowing the operator to only worry about getting the right code, and not having to ILMerge everything together.

Athena comes with a few assemblies able to be loaded out of the box.