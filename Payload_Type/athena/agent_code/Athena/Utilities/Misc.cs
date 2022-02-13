using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace Athena.Utilities
{
    public static class Misc
    {
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
        public static string GetArch()
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
        public static string[] SplitCommandLine(string commandLine)
        {
            bool inQuotes = false;

            return commandLine.Split(c =>
            {
                if (c == '\"')
                    inQuotes = !inQuotes;

                return !inQuotes && c == ' ';
            })
                              .Select(arg => arg.Trim().TrimMatchingQuotes('\"'))
                              .Where(arg => !string.IsNullOrEmpty(arg)).ToArray<string>();
        }
        
        /// <summary>
        /// Split a string
        /// </summary>
        public static IEnumerable<string> Split(this string str,
                                        Func<char, bool> controller)
        {
            int nextPiece = 0;

            for (int c = 0; c < str.Length; c++)
            {
                if (controller(str[c]))
                {
                    yield return str.Substring(nextPiece, c - nextPiece);
                    nextPiece = c + 1;
                }
            }

            yield return str.Substring(nextPiece);
        }

        /// <summary>
        /// Identify where quotes are to support proper split parsing
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="quote">Quote character</param>
        public static string TrimMatchingQuotes(this string input, char quote)
        {
            if ((input.Length >= 2) &&
                (input[0] == quote) && (input[input.Length - 1] == quote))
                return input.Substring(1, input.Length - 2);

            return input;
        }

        /// <summary>
        /// Base64 encode a string and return the encoded string
        /// </summary>
        /// <param name="plainText">String to encode</param>
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
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
        public static string Base64Decode(string base64EncodedData)
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

        /// <summary>
        /// Append bytes to a file
        /// </summary>
        /// <param name="path">Path to write to</param>
        /// <param name="bytes">Bytes to write</param>
        public static void AppendAllBytes(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }
        /// <summary>
        /// Write a debug message to the current standard out
        /// </summary>
        /// <param name="message">Message to write</param>
        public static void WriteDebug(string message)
        {
#if DEBUG
            Console.ForegroundColor = ConsoleColor.White;
            StackTrace stackTrace = new StackTrace();
            Console.WriteLine($"[{stackTrace.GetFrame(1).GetMethod().Name}] {message}");
#endif
        }
        
        /// <summary>
        /// Write an error message to the current standard out
        /// </summary>
        /// <param name="message">Message to write</param>
        public static void WriteError(string message)
        {
#if DEBUG
            Console.ForegroundColor = ConsoleColor.Red;
            StackTrace stackTrace = new StackTrace();
            Console.WriteLine($"[{stackTrace.GetFrame(1).GetMethod().Name}] {message}", Console.ForegroundColor);
#endif
        }
        public static int getIntegrity()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                bool isAdmin;
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                }

                if (isAdmin)
                {
                    return 3;
                }
                else
                {
                    return 2;
                }
            }
            else
            {

                try
                {
                    if (Pinvoke.geteuid() == 0)
                    {
                        return 3;
                    }
                    else
                    {
                        return 2;
                    }
                }
                catch
                {
                    return 0;
                }
            }
        }
    }
}
