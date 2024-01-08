def get_coff_commands():
    return ["nanorubeus", "add-machine-account","ask-creds","delete-machine-account","get-machine-account-quota","kerberoast","klist","adcs-enum", "driver-sigs", "get-password-policy","net-view","sc-enum", "schtasks-enum","schtasks-query","vss-enum","windowlist","wmi-query","add-user-to-group","enable-user","office-tokens","sc-config","sc-create","sc-delete","sc-start","sc-stop","schtasks-run", "schtasks-stop","set-user-pass","patchit"]

def get_inject_shellcode_commands():
    return ["inject-assembly"]

def get_ds_commands():
    return ["ds-query", "ds-connect"]

def get_unloadable_commands():
    return get_ds_commands() + get_coff_commands() + get_inject_shellcode_commands()   