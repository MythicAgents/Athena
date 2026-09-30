
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent.Utilities
{
    public static class Misc
    {
        private static Random random = new Random(DateTime.Now.GetHashCode());
        /// <summary>
        /// Calculate the current sleep time until next check-in
        /// </summary>
        /// <param name="sleep">Time to sleep in seconds</param>
        /// <param name="jitter">Jitter percentage</param>
        public static int GetSleep(int sleep, int jitter)
        {
            Random rand = new Random();
            return rand.Next(Convert.ToInt32(sleep - (sleep * (jitter * 0.01))), Convert.ToInt32(sleep + (sleep * (jitter * 0.01))));
        }

        /// <summary>
        /// Get the architecture of the host
        /// </summary>
        public static string GetArch() => Environment.Is64BitOperatingSystem ? "x64" : "x86";

        /// <summary>
        /// Split command line string into a proper args array
        /// Credit @daniel-earwicker https://stackoverflow.com/users/27423/daniel-earwicker
        /// https://stackoverflow.com/questions/298830/split-string-containing-command-line-parameters-into-string-in-c-sharp
        /// </summary>
        /// <param name="commandLine">Command line string to split</param>
        public static string[] SplitCommandLine(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return Array.Empty<string>();

            var retval = new List<string>();
            string current = string.Empty;
            bool insideDoubleQuote = false;
            bool insideSingleQuote = false;

            foreach (char c in str)
            {
                if (c == ' ' && !insideDoubleQuote && !insideSingleQuote)
                {
                    if (!string.IsNullOrWhiteSpace(current))
                        retval.Add(current.Trim());
                    current = string.Empty;
                }
                if (c == '"') insideDoubleQuote = !insideDoubleQuote;
                if (c == '\'') insideSingleQuote = !insideSingleQuote;
                current += c;
            }

            if (!string.IsNullOrWhiteSpace(current))
                retval.Add(current.Trim());

            return retval.ToArray();
        }

        /// <summary>
        /// Base64 encode a string and return the encoded string
        /// </summary>
        /// <param name="plainText">String to encode</param>
        public static string Base64Encode(string plainText) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));

        /// <summary>
        /// Base64 encode a byte array and return the encoded string
        /// </summary>
        /// <param name="bytes">Byte array to encode</param>
        public static string Base64Encode(byte[] bytes) => Convert.ToBase64String(bytes);

        /// <summary>
        /// Base64 decode a string and return the decoded string
        /// </summary>
        /// <param name="base64EncodedData">String to decode</param>
        public static string Base64Decode(string base64EncodedData) =>
            Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedData));

        /// <summary>
        /// Base64 decode a string and return it as a byte array
        /// </summary>
        /// <param name="base64EncodedData">String to decode</param>
        public static byte[] Base64DecodeToByteArray(string base64EncodedData) =>
            Convert.FromBase64String(base64EncodedData);

        /// <summary>
        /// Append bytes to a file
        /// </summary>
        /// <param name="path">Path to write to</param>
        /// <param name="bytes">Bytes to write</param>
        public static async Task AppendAllBytes(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.Append);
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }

        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes);
        }

        public static IEnumerable<string> SplitByLength(this string str, int maxLength)
        {
            for (int index = 0; index < str.Length; index += maxLength)
            {
                yield return str.Substring(index, Math.Min(maxLength, str.Length - index));
            }
        }

        public static Dictionary<string, string> ConvertJsonStringToDict(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new Dictionary<string, string>();

            using JsonDocument jdoc = JsonDocument.Parse(json);
            return jdoc.RootElement.EnumerateObject()
                .ToDictionary(node => node.Name, node => node.Value.ToString() ?? "");
        }

        public static void CheckExpiration(DateTime killdate)
        {
            if (killdate < DateTime.Now)
            {
                Debug.WriteLine($"[{DateTime.Now}] Killdate reached, exiting.");
                Environment.Exit(0);
            }
        }

        public static int GenerateRandomNumber() => random.Next();

        public static int GenerateSmallerRandomNumber() => random.Next(0, 15);

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static byte[] CombineByteArraysOld(byte[] array1, byte[] array2)
        {
            if (array1 == null)
                return array2;
            if (array2 == null)
                return array1;

            byte[] combinedArray = new byte[array1.Length + array2.Length];
            Buffer.BlockCopy(array1, 0, combinedArray, 0, array1.Length);
            Buffer.BlockCopy(array2, 0, combinedArray, array1.Length, array2.Length);

            return combinedArray;
        }

        public static byte[] CombineByteArrays(byte[] array1, byte[] array2)
        {
            if (array1 is null || array1.Length == 0)
                return array2 ?? Array.Empty<byte>();
            if (array2 is null || array2.Length == 0)
                return array1;

            byte[] result = GC.AllocateUninitializedArray<byte>(array1.Length + array2.Length);

            var span = result.AsSpan();
            array1.AsSpan().CopyTo(span);
            array2.AsSpan().CopyTo(span.Slice(array1.Length));

            return result;
        }

        public static bool CheckListValues<T>(List<T> list1, List<T> list2) =>
            list2.All(list1.Contains);

        public static byte[] CombineArrays(byte[] array1, byte[] array2) =>
            array1.Concat(array2).ToArray();

        public static Encoding GetEncoding(byte[] fileContents)
        {
            var bom = new byte[4];
            Array.Copy(fileContents, bom, Math.Min(fileContents.Length, 4));

            // Analyze the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe && bom[2] == 0 && bom[3] == 0) return Encoding.UTF32; //UTF-32LE
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return new UTF32Encoding(true, true);  //UTF-32BE

            return Encoding.ASCII;
        }
    }
}
