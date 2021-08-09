using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Athena.Utilities
{
    public static class Misc
    {
        public static int GetSleep(int sleep, int jitter)
        {
            Random rand = new Random();
            return rand.Next(Convert.ToInt32(sleep - (sleep * (jitter * 0.01))), Convert.ToInt32(sleep + (sleep * (jitter * 0.01))));
        }
        public static string GetArch()
        {
            if (Environment.Is64BitOperatingSystem)
                return "x64";
            else
                return "x86";
        }


        //Credit @daniel-earwicker https://stackoverflow.com/users/27423/daniel-earwicker
        //From: https://stackoverflow.com/questions/298830/split-string-containing-command-line-parameters-into-string-in-c-sharp

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
        public static string TrimMatchingQuotes(this string input, char quote)
        {
            if ((input.Length >= 2) &&
                (input[0] == quote) && (input[input.Length - 1] == quote))
                return input.Substring(1, input.Length - 2);

            return input;
        }
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }
        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }
        public static byte[] Base64DecodeToByteArray (string base64EncodedData)
        {
            return Convert.FromBase64String(base64EncodedData);
        }
    }
}
