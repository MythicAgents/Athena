using Invoker.Dynamic;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Agent
{
    class IAT
    {
        private sealed class ImportEntry
        {
            internal IntPtr FunctionAddress;
            internal readonly List<IntPtr> References = new List<IntPtr>();

            internal ImportEntry(IntPtr functionAddress)
            {
                FunctionAddress = functionAddress;
            }
        }

        private readonly Dictionary<string, ImportEntry> iat_entries = new Dictionary<string, ImportEntry>();
        private delegate IntPtr LlDelegate(string lpFileName);
        private delegate IntPtr GPADelegate(IntPtr hModule, string lpProcName);

        public IntPtr Resolve(string dll_name, string func_name, IntPtr reference_address)
        {
            string key = GetKey(dll_name, func_name);
            if (!this.iat_entries.TryGetValue(key, out ImportEntry entry))
            {
                object[] llParams = new object[] { dll_name };
                IntPtr dll_handle = Generic.InvokeFunc<IntPtr>(Resolver.GetFunc("ll"), typeof(LlDelegate), ref llParams);

                object[] gpaParams = new object[] { dll_handle, func_name };
                IntPtr func_ptr = Generic.InvokeFunc<IntPtr>(Resolver.GetFunc("gpa"), typeof(GPADelegate), ref gpaParams);
                if (func_ptr == IntPtr.Zero)
                {
                    throw new Exception($"Unable to resolve {func_name} from {dll_name}");
                }

                entry = new ImportEntry(func_ptr);
                this.iat_entries.Add(key, entry);
            }

            return AddReference(entry, reference_address);
        }

        public void Add(string dll_name, string func_name, IntPtr func_address)
        {
            string key = GetKey(dll_name, func_name);
            if (this.iat_entries.TryGetValue(key, out ImportEntry existingEntry))
            {
                if (existingEntry.FunctionAddress != func_address)
                {
                    throw new Exception($"IAT entry {key} already exists with a different address");
                }
                return;
            }

            this.iat_entries.Add(key, new ImportEntry(func_address));
        }

        public IntPtr AddReference(string dll_name, string func_name, IntPtr reference_address)
        {
            string key = GetKey(dll_name, func_name);
            if (!this.iat_entries.TryGetValue(key, out ImportEntry entry))
            {
                throw new Exception($"Unable to add IAT reference for {key} as no entry exists");
            }

            return AddReference(entry, reference_address);
        }

        public void Update(string dll_name, string func_name, IntPtr func_address)
        {
            string key = GetKey(dll_name, func_name);
            if (!this.iat_entries.TryGetValue(key, out ImportEntry entry))
            {
                throw new Exception($"Unable to update IAT entry for {key} as no entry exists");
            }

            entry.FunctionAddress = func_address;
            foreach (IntPtr reference in entry.References)
            {
                Marshal.WriteIntPtr(reference, func_address);
            }
        }

        internal void Clear()
        {
            this.iat_entries.Clear();
        }

        private static IntPtr AddReference(ImportEntry entry, IntPtr reference_address)
        {
            Marshal.WriteIntPtr(reference_address, entry.FunctionAddress);
            entry.References.Add(reference_address);
            return reference_address;
        }

        private static string GetKey(string dll_name, string func_name)
        {
            return dll_name + "$" + func_name;
        }
    }
}
