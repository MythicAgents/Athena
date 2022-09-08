+++
title = "reset-assembly-context"
chapter = false
weight = 10
hidden = false
+++

## Summary
Reset the AssemblyLoadContext for `Athena`
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander  

### Arguments
None

## Usage

```
reset-assembly-context
```

## Detailed Summary
All assemblies are loaded into the same AssemblyLoadContext, sometimes they get to a point where libraries are interfering with each other. `reset-assembly-context` unloads all the current assemblies giving the operator a blank slate from which to operate on.