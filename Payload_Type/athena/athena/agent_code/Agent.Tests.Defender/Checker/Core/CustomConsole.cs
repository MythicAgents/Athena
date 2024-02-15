using System;

namespace Agent.Tests.Defender.Checker.Core
{
    class CustomConsole
    {
        public static void WriteOutput(string output)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Out.WriteLine($"[+] {output}");
            Console.ResetColor();
        }

        public static void WriteError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"[x] {error}");
            Console.ResetColor();
        }

        public static void WriteDebug(string debug)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Out.WriteLine($"[*] {debug}");
            Console.ResetColor();
        }

        public static void WriteThreat(string threat)
        {
            var msg = string.Format($"[!] {threat}");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Out.WriteLine(msg);
            Console.ResetColor();
        }
    }
}