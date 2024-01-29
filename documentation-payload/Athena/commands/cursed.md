+++
title = "crop"
chapter = false
weight = 10
hidden = false
+++

## Summary
Interactive electron exploitation command
  
- Needs Admin: False  
- Version: 1  
- Author: @checkymander

### Arguments

#### debug_port

- Description: The default debug port to use when spawning electron applications
- Required Value: False  
- Default Value: 

#### target

- Description: The target to set for the cursed target 
- Required Value: False  
- Default Value:  

#### payload

- Description: Use a custom payload for cursed rather than the built-in one
- Required Value: False  
- Default Value:  

#### cmdline

- Description: The cmdline to append to the spawn
- Required Value: False  
- Default Value:  

#### parent

- Description: Set the parent process ID to this value
- Required Value: False  
- Default Value:  

 #### path

- Description: Overwrite the default path for the electron application
- Required Value: False  
- Default Value:  

## Usage
```
    cursed [-path C:\\Users\\checkymander\\chrome] [-parent 1234] [-cmdline "nothing to see here"] [-target ws://127.0.0.1:1234] [-debug_port 9222] 
    Commands:
        cursed
            Enumerates a spawned electron process via the local debugging port for extensions with permissions suitable for CursedChrome. 
                If a payload is specified it will use that, if not, it will use the built-in payload with the target setting 

        set [config] [value]
            Set's a configuration value. For cursed commands
                set debug_port 2020 //Set's the port to be used for the electron debug port
                set debug true/false //Displays debug output when running commands
                set payload <payload> //Sets the payload to be used
                set target ws[s]://target:port //Sets the target for the default payload, this parameter is ignored if the payload has been manually set
                set cmdline "--user-data-dir=C:\\Users\\checkymander\\" //Commandline to append to the target process
                set parent <pid>

        get [target|payload|extensions|debug-port|debug|cmdline]
            Get's the value of the configuration parameter and prints it to output

        spawn [chrome|edge]
            Spawn a new instance of chrome or edge with the configured port 

        cookies
            Download cookies using the remote debugging port
```


## Detailed Summary