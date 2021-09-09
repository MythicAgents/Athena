+++
title = "Development"
chapter = false
weight = 3
pre = "<b>3. </b>"
+++

## Development Environment

For plugin development, use .NET 5.0

## Adding Plugins

Open the AthenaPlugins.sln in the `agent_code` directory.

Once `Visual Studio` loads right-click the Solution in the Solution Explorer and select `Add` then select `New Project`

Select a `Class library` targeting .NET Standard or .NET Core

Click `Next` and name your project the name of your new plugin

Look for the `PluginTemplate` project and double-click on `PluginName.cs` copy this template into your existing `Program.cs`

Rename your Program.cs to `<pluginname>.cs`

Whatever code you will need to perform will be handled in the `Execute` function. 

This function should return a string containing the plugin output.

Right-click your project name and select `Properties`. Select `Build Events` and paste the following code into your `Post-build event command line`

```
mv $(TargetPath) $(SolutionDir)\bin\
```

Finally, add the relevant `pluginname.py` in the `/Athena/mythic/agent_functions` directory as outlined here:
https://docs.mythic-c2.net/customizing/payload-type-development/commands

## Adding C2 Profiles
- Copy the C2ProfileEmpty.txt file and paste it into the `/Athena/Config/MythicConfig.cs` folder
- The `MythicConfig` class will contain the basic information needed for mythic to function 
- Rename `C2Profile` to a class name to describe your C2 Profile, in addition change the class of `currentConfig` to this new class
- At minimum, this class should implement a `Send` function that returns the string provided by the mythic server
```
public async Task<string> Send(object obj){
    //Send to mythic server
    
    //Read response from mythic server
    
    //return base response to MythicClient.cs
}
```
- If you would like to add SMBServer support to your ExampleProfile, replace the contents of `SMBConfig.cs` with the contents of `SMBServer.txt`
- The basic C2Profile skeleton should be placed in a text file with the other C2Profiles
- Update `builder.py` to replace the contents of `MythicConfig.cs` at build-time