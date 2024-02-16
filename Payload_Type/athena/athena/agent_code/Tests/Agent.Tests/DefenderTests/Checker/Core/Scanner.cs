using System;

namespace Agent.Tests.Defender.Checker.Core
{
    public class Scanner
    {
        public static bool Malicious = false;
        public static bool Complete = false;

        public virtual byte[] HalfSplitter(byte[] originalarray, int lastgood, out string badBytes)
        {
            badBytes = string.Empty;
            var splitArray = new byte[(originalarray.Length - lastgood) / 2 + lastgood];

            if (originalarray.Length == splitArray.Length + 1)
            {
                var msg = string.Format("Identified end of bad bytes at offset 0x{0:X}", originalarray.Length);

                CustomConsole.WriteThreat(msg);

                byte[] offendingBytes = new byte[256];

                if (originalarray.Length < 256)
                {
                    Array.Resize(ref offendingBytes, originalarray.Length);
                    Buffer.BlockCopy(originalarray, originalarray.Length, offendingBytes, 0, originalarray.Length);
                }
                else
                {
                    Buffer.BlockCopy(originalarray, originalarray.Length - 256, offendingBytes, 0, 256);
                }

                badBytes = Helpers.HexDump(offendingBytes);
                Complete = true;
            }

            Array.Copy(originalarray, splitArray, splitArray.Length);
            return splitArray;
        }

        public virtual byte[] Overshot(byte[] originalarray, int splitarraysize)
        {
            var newsize = (originalarray.Length - splitarraysize) / 2 + splitarraysize;

            if (newsize.Equals(originalarray.Length - 1))
            {
                Complete = true;

                if (Malicious)
                {
                    CustomConsole.WriteError("File is malicious, but couldn't identify bad bytes");
                }
            }

            var newarray = new byte[newsize];
            Buffer.BlockCopy(originalarray, 0, newarray, 0, newarray.Length);

            return newarray;
        }
    }
}