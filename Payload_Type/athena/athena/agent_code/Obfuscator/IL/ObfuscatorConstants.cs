namespace Obfuscator.IL;

internal static class ObfuscatorConstants
{
    /// <summary>
    /// Assembly name prefixes that are never renamed.
    /// Covers .NET runtime, Microsoft SDK, and known third-party bundles
    /// embedded in the self-contained single-file publish output.
    /// </summary>
    internal static readonly string[] SkipPrefixes =
    [
        "System.", "Microsoft.", "runtime.",
        "Autofac", "IronPython", "BouncyCastle",
        "H.", "Renci", "Mono.", "NamedPipe"
    ];
}
