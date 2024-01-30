## Changelog
01/30/2024 - 2.0 Release
- Refactor of entire agent
    - Core Agent now makes use of Dependency Injection
    - Now every command is hot-loadable
    - Plugins can take advantage of Mythic hooks, such as Upload, Download, and Delegate passing
    - Plugins can now modify configuration options of the agent
    - Spawner implementation has been standardized and made available to all plugins
    - Unnecessary projects have been removed or rewritten
- Indirect API Invoking
    - Using the DInvoke implementation of HInvoke
    - Added to `inject-shellcode`, `coff`, and `token` commands
- Interactive Tasking
    - `shell` and `ssh` have been made to be interactive, allowing more flexibility with your execution options
    - `cursed` command has been added, allowing you to execute javascript and dump cookies from Electron-based applications
- Plugin Changes
    - `exec` has replaced the old `shell` functionality, allowing more flexibility with how processes are spawned
    - `config` has replaced `sleep` allowing you to configure more agent options rather than just sleep
    - `http-server` has been added, allowing you to host servers via HTTP on the agent directly
    - `port-bender` has been added, allowing you to shape traffic on the agent
    - `reg` has added support for non-string data types
    - `inject-shellcode` can now switch between techniques using `config`
    - `socks` has been re-added and improved, is also not hot-loadable
    - `rportfwd` has been improvded
    - `smb` has now become a plugin, and can be loaded or not
    - `token` now has the ability to steal a token from an existing process
- Agent Changes
    - Athena can now have its assemblies obfuscated using Obfuscar
    - Added more build-options for removing stack traces, invariant globalization, and usesystemresourcekeys
    - Added an additional build option for macOS bundles
    - Added support for hiding of console for Windows executables

06/13/2023 - 1.0 Release
- Refactor profile code
  - Support for multiple profiles
  - Support for "pushing" profiles when available
- BOF Support!
- Reverse Portfwarding
- Improved SMB communication
  - SMB Communication is now lighter on the wire
  - SMB links now support a one-to-many communications
  - SMB links can be linked and unlinked as necessary
- Improved SOCKS5 communication
- Added the following capabilities
  - inject-assembly
  - inject-shellcode
  - ps now returns parent process information
  - ls has improved support for the filebrowser
  - ability to hot swap profiles
  - screenshot
  - token
  - timestomp
  - unlink
  - coff
    - Can be used to load BOFs
    - Athena comes preloaded with a large number of BOF's available

09/08/2022 - 0.2 Release
 - Refactored base agent code
 - Refactor of plugin loading capabilities
 - Improvements to SMB C2 Profile
 - Stability Improvements
 - Added support for `ps` and `ls` mythic hooks
 - Added the following capabilities
 	- token
 	- farmer & crop
 	- load-module
 	- ds
 	- sftp
 	- ssh
 	- get-sessions
 	- get-localgroup
 	- get-shares
 	- shellcode
 	- test-port
 	- win-enum-resources
 	- reg

02/15/22 - 0.1 Release
 - Initial Release


