param(
    [string[]]$profiles, 
    [string[]]$commands, 
    [switch]$nativeaot, 
    [switch]$compressed, 
    [string]$forwarder, 
    [switch]$selfcontained, 
    [switch]$singlefile, 
    [string]$configuration, 
    [switch]$trimmed,
    [string]$rid,
    [string]$os,
    [switch]$help
    )

if($help){
    Write-Host "./build.ps1 -profiles <> -forwarder <> -commands <> -nativeaot -compressed -forwarder <> -selfcontained -singlefile -configuration <> -trimmed -rid <> -os <> -help"
    Write-Host ""
    Write-Host "Profiles (Comma Separated) `r`n`thttp, websocket, smb`r`n"
    Write-Host "Commands (Comma Separated) `r`n`tSupported commands or all `r`n"
    Write-Host "Forwarder (Select One)`r`n`tsmb, none`r`n"
    Write-Host "Configuration (Select One)`r`n`tRelease, Debug`r`n"
    Write-Host "RID (Select One)`r`n`twin-x64, linux-x64, osx-x64`r`n"
    Write-Host "OS (Select One)`r`n`tWindows, Linux, MacOS`r`n"
    Write-Host "Compressed (Switch)`r`n`tCompresses the binary`r`n"
    Write-Host "Self Contained (Switch)`r`n`tMakes the binary self contained`r`n"
    Write-Host "Single File (Switch)`r`n`tMakes the binary a single file`r`n"
    Write-Host "Trimmed (Switch)`r`n`tTrims the binary`r`n"
    Write-Host "NativeAot (Switch)`r`n`tCompiles as a native binary, if selected trimmed will be set to true`r`n"
    Write-Host "Help (Switch)`r`n`tDisplays this help message`r`n"
    Write-Host ""
    Write-Host "Compile a native AOT Athena with all commands, HTTP Profile, and SMB Forwarer for Windows: `r`n`t./build.ps1 -profiles http -commands all -nativeaot -forwarder smb -singlefile -os `"windows`""
    Write-Host ""
    Write-Host "Default Recommended:`r`n`t./build.ps1 -commands all -profile http -forwarder none -compressed -selfcontained -singlefile -configuration Release -rid win-x64 -os windows"
    Exit
}


    if($profiles -eq ""){
        Write-Host "Profiles need to be specified"
        Write-Host "./build.ps1 -profiles http,websocket -commands ls,whoami,cd,coff -trimmed -nativeaot -compressed -forwarder"
        Exit
    }

#Build NativeAOT: ./build.ps1 -profiles http -commands all  -nativeaot -forwarder smb -singlefile -os "windows"
#Build Defaults: ./build.ps1 -profiles http -commands all -forwarder smb -os "windows" -compressed -singlefile -selfcontained -configuration Release -

Write-Host "RID"
if($rid -eq ""){
    $rid = "win-x64"
}
Write-Host "`t$rid`r`n"

$env:AthenaConstants = "";

Write-Host "Adding Profiles"
foreach($prof in $profiles) {
    Write-Host "`t$prof"
    if($prof.ToLower() -eq "smb"){
        $env:AthenaConstants += "SMBPROFILE;"
    }else{
        $env:AthenaConstants += $prof.ToUpper() + ";"
    }
}
Write-Host ""

Write-Host "Selecting OS"
if($os.ToLower() -eq "windows"){
    Write-Host "`tWindows"
    $env:AthenaConstants += "WINBUILD;"
}
elseif($os.ToLower() -eq "linux"){
    Write-Host "`tLinux"
    $env:AthenaConstants += "NIXBUILD;"
}
elseif($os.ToLower() -eq "macos"){
    Write-Host "`tMacOS"
    $env:AthenaConstants = "MACBUILD;"
}
Write-Host ""

Write-Host "Adding commands."
foreach($command in $commands) {
    if($command -eq "all"){
        Write-Host "`tAll commands selected."
        $env:AthenaConstants += "LS;UPTIME;PS;MV;GETSESSIONS;ARP;GETSHARES;DS;DRIVES;GETCLIPBOARD;GETLOCALGROUP;WHOAMI;REG;IFCONFIG;PWD;SHELL;MKDIR;TIMESTOMP;TESTPORT;HOSTNAME;ENV;CP;WINENUMRESOURCES;NSLOOKUP;CAT;CROP;FARMER;PATCH;RM;SFTP;KILL;CD"
    }
    else{
        Write-Host "`t$command"
        $env:AthenaConstants += $command.ToUpper() + ";"
    }
}
Write-Host ""

Write-Host "Binary Type"
if($nativeaot){
    Write-Host "`tNative"
    $env:AthenaConstants += "NATIVEAOT;"
    $singlefile = $false;
    $trimmed = $true
}
else{
    Write-Host "`tDynamic"
    $env:AthenaConstants += "DYNAMIC;"
}
Write-Host ""

Write-Host "Fowrwarder"
if($forwarder.ToUpper() -eq "SMB"){
    Write-Host "`tSMB"
    $env:AthenaConstants += "SMBFWD;"
}
else{
    Write-Host "`tNone"
    $env:AthenaConstants += "EMPTYFWD;"
}
Write-Host ""
Write-Host "Configuration"
if($configuration -eq ""){
    Write-Host "`tRelease"
    $configuration = "Release"
}
else{
    Write-Host "`t$configuration"
}
Write-Host ""
Write-Host "Compressed:`r`n`t $compressed"
Write-Host ""
Write-Host "Single File:`r`n`t $singlefile"
Write-Host ""
Write-Host "Self Contained:`r`n`t $selfcontained"
Write-Host ""
Write-Host "Trimmed:`r`n`t $trimmed"
Write-Host ""

Write-Host "Final Parameters:`r`n`t" $env:AthenaConstants
# $processOptions = @{
#     FilePath = "dotnet.exe"
#     ArgumentList = "publish Athena -r $rid -c $configuration --self-contained $selfcontained /p:PublishSingleFile=$singlefile /p:EnableCompressionInSingleFile=$compressed /p:PublishTrimmed=$trimmed /p:PublishAOT=$nativeaot /p:DebugType=None /p:DebugSymbols=false /p:SolutionDir=./"
#     WorkingDirectory = "."
# }

Write-Host "Build Command:"
Write-Host "`tdotnet publish Athena -r $rid -c $configuration --self-contained $selfcontained /p:PublishSingleFile=$singlefile /p:EnableCompressionInSingleFile=$compressed /p:PublishTrimmed=$trimmed /p:PublishAOT=$nativeaot /p:DebugType=None /p:DebugSymbols=false /p:SolutionDir=`".`""

dotnet publish Athena -r $rid -c $configuration --self-contained $selfcontained /p:PublishSingleFile=$singlefile /p:EnableCompressionInSingleFile=$compressed /p:PublishTrimmed=$trimmed /p:PublishAOT=$nativeaot /p:DebugType=None /p:DebugSymbols=false /p:SolutionDir="."