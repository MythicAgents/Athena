using Obfuscator.IL.Transforms;

namespace Obfuscator.IL;

/// <summary>
/// Orchestrates IL-level obfuscation transforms on a compiled .NET assembly.
/// Applies metadata mangling, writing the result back to the same path.
/// </summary>
public sealed class ILRewriter
{
    /// <summary>
    /// Applies IL transforms to <paramref name="inputDllPath"/> in-place.
    /// </summary>
    /// <param name="inputDllPath">Path to the assembly to rewrite.</param>
    /// <param name="seed">Random seed for deterministic obfuscation.</param>
    /// <param name="mapPath">
    /// Optional path to a deobfuscation map JSON file. If provided, IL-level
    /// rename info is merged into the file (creating it if absent).
    /// </param>
    public void Rewrite(string inputDllPath, int seed, string? mapPath)
    {
        var bytes = File.ReadAllBytes(inputDllPath);
        var searchDir = Path.GetDirectoryName(
            Path.GetFullPath(inputDllPath));

        var mmt = new MetadataManglingTransform(seed);
        bytes = mmt.Transform(bytes, searchDir);

        File.WriteAllBytes(inputDllPath, bytes);

        if (mapPath is not null)
        {
            var map = File.Exists(mapPath)
                ? DeobfuscationMap.LoadFromFile(mapPath)
                : new DeobfuscationMap();
            map.MetadataRenames = mmt.GetRenameMappings();
            map.SaveToFile(mapPath);
        }
    }
}
