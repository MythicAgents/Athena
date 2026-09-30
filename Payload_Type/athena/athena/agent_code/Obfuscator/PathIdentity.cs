namespace Obfuscator;

internal static class PathIdentity
{
    internal static StringComparer Comparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    internal static StringComparison Comparison { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    internal static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    internal static bool Equals(string left, string right) =>
        Comparer.Equals(Normalize(left), Normalize(right));

    internal static bool IsWithin(string path, string directory)
    {
        var normalizedPath = Normalize(path);
        var normalizedDirectory = Normalize(directory);
        if (Comparer.Equals(normalizedPath, normalizedDirectory))
            return true;

        var prefix = normalizedDirectory + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(prefix, Comparison);
    }
}
