using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Athena.Utilities
{
    public static class Misc
    {
        private static Random random = new Random(DateTime.Now.GetHashCode());
        /// <summary>
        /// Calculate the current sleep time until next check-in
        /// </summary>
        /// <param name="sleep">Time to sleep in seconds</param>
        /// <param name="jitter">Jitter percentage</param>
        public static async Task<int> GetSleep(int sleep, int jitter)
        {
            Random rand = new Random();
            return rand.Next(Convert.ToInt32(sleep - (sleep * (jitter * 0.01))), Convert.ToInt32(sleep + (sleep * (jitter * 0.01))));
        }

        /// <summary>
        /// Get the architecture of the host
        /// </summary>
        public static async Task<string> GetArch()
        {
            if (Environment.Is64BitOperatingSystem)
                return "x64";
            else
                return "x86";
        }

        /// <summary>
        /// Split command line string into a proper args array
        /// Credit @daniel-earwicker https://stackoverflow.com/users/27423/daniel-earwicker
        /// https://stackoverflow.com/questions/298830/split-string-containing-command-line-parameters-into-string-in-c-sharp
        /// </summary>
        /// <param name="commandLine">Command line string to split</param>
        public async static Task<string[]> SplitCommandLine(string str)
        {
            var retval = new List<string>();
            if (String.IsNullOrWhiteSpace(str)) return retval.ToArray();
            int ndx = 0;
            string s = String.Empty;
            bool insideDoubleQuote = false;
            bool insideSingleQuote = false;

            while (ndx < str.Length)
            {
                if (str[ndx] == ' ' && !insideDoubleQuote && !insideSingleQuote)
                {
                    if (!String.IsNullOrWhiteSpace(s.Trim())) retval.Add(s.Trim());
                    s = String.Empty;
                }
                if (str[ndx] == '"') insideDoubleQuote = !insideDoubleQuote;
                if (str[ndx] == '\'') insideSingleQuote = !insideSingleQuote;
                s += str[ndx];
                ndx++;
            }
            if (!String.IsNullOrWhiteSpace(s.Trim())) retval.Add(s.Trim());
            return retval.ToArray();
        }

        /// <summary>
        /// Base64 encode a string and return the encoded string
        /// </summary>
        /// <param name="plainText">String to encode</param>
        public static async Task<string> Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
        
        /// <summary>
        /// Base64 encode a byte array and return the encoded string
        /// </summary>
        /// <param name="bytes">Byte array to encode</param>
        public static async Task<string> Base64EncodeAsync(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }
        /// <summary>
        /// Base64 encode a byte array and return the encoded string
        /// </summary>
        /// <param name="bytes">Byte array to encode</param>
        public static string Base64Encode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Base64 decode a string and return the decoded string
        /// </summary>
        /// <param name="base64EncodedData">String to decode</param>
        public static async Task<string> Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }
        
        /// <summary>
        /// Base64 decode a string and return it as a byte array
        /// </summary>
        /// <param name="base64EncodedData">String to decode</param>
        public static byte[] Base64DecodeToByteArray (string base64EncodedData)
        {
            return Convert.FromBase64String(base64EncodedData);
        }
        public static async Task<byte[]> Base64DecodeToByteArrayAsync(string base64EncodedData)
        {
            return Convert.FromBase64String(base64EncodedData);
        }

        /// <summary>
        /// Append bytes to a file
        /// </summary>
        /// <param name="path">Path to write to</param>
        /// <param name="bytes">Bytes to write</param>
        public static async Task AppendAllBytes(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Append))
            {
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }
        }

        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                return Convert.ToHexString(hashBytes);
            }
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
            if (String.IsNullOrEmpty(json))
            {
                return new Dictionary<string, string>();
            }
            else
            {
                Dictionary<string, string> parameters = new();
                JsonDocument jdoc = JsonDocument.Parse(json);

                foreach (var node in jdoc.RootElement.EnumerateObject())
                {
                    parameters.Add(node.Name, node.Value.ToString() ?? "");
                }
                return parameters;
            }
        }
        public static void CheckExpiration(DateTime killdate)
        {
            if (killdate < DateTime.Now)
            {
                Debug.WriteLine($"[{DateTime.Now}] Killdate reached, exiting.");
                Environment.Exit(0);
            }
        }

        public static int GenerateRandomNumber()
        {
            return random.Next();
        }
        public static int GenerateSmallerRandomNumber()
        {
            return random.Next(0,15);
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
