def get_coff_commands():
    return [
            "adcs-enum",
            "add-machine-account",
            "add-user-to-group",
            "ask-creds",
            "delete-machine-account",
            "driver-sigs",
            "enable-user",
            "get-machine-account-quota",
            "get-password-policy",
            "kerberoast",
            "klist", 
            "nanorubeus", 
            "net-view",
            "office-tokens",
            "patchit",
            "schtasks-create", 
            "schtasks-delete",
            "schtasks-enum",
            "schtasks-query",
            "schtasks-run", 
            "schtasks-stop",
            "set-user-pass",
            "sc-config",
            "sc-create",
            "sc-delete",
            "sc-enum", 
            "sc-start",
            "sc-stop",
            "vss-enum",
            "windowlist",
            "wmi-query",
            ]

def get_inject_shellcode_commands():
    return ["inject-assembly"]

def get_ds_commands():
    return ["ds-query", "ds-connect"]

def get_builtin_commands():
    return ["load", "load-assembly"]

def get_unloadable_commands():
    return get_ds_commands() + get_coff_commands() + get_inject_shellcode_commands() + get_nidhogg_commands() + get_builtin_commands()

def get_nidhogg_commands():
    return ["nidhogg-disableetwti", 
            "nidhogg-dumpcreds", 
            "nidhogg-elevateprocess",
            "nidhogg-enableetwti", 
            "nidhogg-hidedriver", 
            "nidhogg-hideport", 
            "nidhogg-hideprocess", 
            "nidhogg-hideregistrykey",
            "nidhogg-hideregistryvalue",
            "nidhogg-hidethread", 
            "nidhogg-injectdll", 
            "nidhogg-protectfile", 
            "nidhogg-protectprocess",
            "nidhogg-protectregistrykey", 
            "nidhogg-protectregistryvalue",
            "nidhogg-protectthread", 
            "nidhogg-unhidedriver", 
            "nidhogg-unhideport", 
            "nidhogg-unhideregistrykey", 
            "nidhogg-unhideregistryvalue", 
            "nidhogg-unhidethread", 
            "nidhogg-unprotectfile", 
            "nidhogg-unprotectprocess", 
            "nidhogg-unprotectregistrykey", 
            "nidhogg-unprotectregistryvalue", 
            "nidhogg-unprotectthread"]