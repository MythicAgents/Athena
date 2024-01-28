#define _AMD64
using Invoker.Dynamic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    class IAT
    {
        private readonly IntPtr iat_addr;
        private int iat_pages;
        private int iat_count;
        private readonly Dictionary<String, IntPtr> iat_entries;
        private delegate IntPtr VaDelegate(IntPtr lpStartAddr, uint size, uint flAllocationType, uint flProtect);
        private delegate bool VfDelegate(IntPtr pAddress, uint size, uint freeType);
        private delegate IntPtr LlDelegate(string lpFileName);
        private delegate IntPtr GPADelegate(IntPtr hModule, string lpProcName);
        private delegate void ZMDelegate(IntPtr dest, int size);
        private delegate bool CTDelegate(IntPtr pAddress, uint size, uint freeType);
        private bool resolved = false;
        public IAT()
        {
            this.iat_pages = 2;

            object[] vaParams = new object[] { IntPtr.Zero, (uint)(this.iat_pages * Environment.SystemPageSize), NativeDeclarations.MEM_COMMIT, NativeDeclarations.PAGE_EXECUTE_READWRITE };
            this.iat_addr = Generic.InvokeFunc<nint>(Resolver.GetFunc("va"), typeof(VaDelegate), ref vaParams);

            //this.iat_addr = NativeDeclarations.VirtualAlloc(IntPtr.Zero, (uint)(this.iat_pages * Environment.SystemPageSize), NativeDeclarations.MEM_COMMIT, NativeDeclarations.PAGE_EXECUTE_READWRITE);
            this.iat_count = 0;
            this.iat_entries = new Dictionary<string, IntPtr>();
        }
        public IntPtr Resolve(string dll_name, string func_name)
        {
            // do we already have it in our IAT table? It not lookup and add
            if (!this.iat_entries.ContainsKey(dll_name + "$" + func_name))
            {
                object[] llParams = new object[] { dll_name };
                IntPtr dll_handle = Generic.InvokeFunc<IntPtr>(Resolver.GetFunc("ll"), typeof(LlDelegate), ref llParams);
                //IntPtr dll_handle = NativeDeclarations.LoadLibrary(dll_name);


                object[] gpaParams = new object[] { dll_handle, func_name };
                IntPtr func_ptr = Generic.InvokeFunc<IntPtr>(Resolver.GetFunc("gpa"), typeof(GPADelegate), ref gpaParams);
                //IntPtr func_ptr = NativeDeclarations.GetProcAddress(dll_handle, func_name);
                if (func_ptr == null || func_ptr.ToInt64() == 0)
                {
                    throw new Exception($"Unable to resolve {func_name} from {dll_name}");
                }
                Add(dll_name, func_name, func_ptr);
            }

            return this.iat_entries[dll_name + "$" + func_name];

        }

        // This can also be called directly for functions where you already know the address (e.g. helper functions)
        public IntPtr Add(string dll_name, string func_name, IntPtr func_address)
        {
            // check we have space in our IAT table
            if (this.iat_count * 8 > (this.iat_pages * Environment.SystemPageSize))
            {
                throw new Exception("Run out of space for IAT entries!");
            }

            Marshal.WriteInt64(this.iat_addr + (this.iat_count * 8), func_address.ToInt64());
            this.iat_entries.Add(dll_name + "$" + func_name, this.iat_addr + (this.iat_count * 8));
            this.iat_count++;
            return this.iat_entries[dll_name + "$" + func_name]; 

        }

        public void Update(string dll_name, string func_name, IntPtr func_address)
        {
            if (!this.iat_entries.ContainsKey(dll_name + "$" + func_name)) throw new Exception($"Unable to update IAT entry for {dll_name + "$" + func_name} as don't have an existing entry for it");
            // Write the new address into our IAT memory. 
            // we don't need to update our internal iat_entries dict as that is just a mapping of name to IAT memory location.

            Marshal.WriteInt64(this.iat_entries[dll_name + "$" + func_name], func_address.ToInt64());
        }

        internal void Clear()
        {
            // zero out memory
            NativeDeclarations.ZeroMemory(this.iat_addr, this.iat_pages * Environment.SystemPageSize);

            // free it
            object[] vfParams = new object[] { this.iat_addr, (uint)0, NativeDeclarations.MEM_RELEASE };
            var result = Generic.InvokeFunc<bool>(Resolver.GetFunc("vf"), typeof(VfDelegate), ref vfParams);
        }
    }
}
