using Obfuscator.IL.Transforms;

namespace Obfuscator.IL;

/// <summary>
/// Orchestrates IL-level obfuscation transforms on a compiled .NET assembly.
/// Applies control flow flattening followed by metadata mangling, writing
/// the result back to the same path.
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
    /// rename and transform info is merged into the file (creating it if absent).
    /// </param>
    public void Rewrite(string inputDllPath, int seed, string? mapPath)
    {
        var bytes = File.ReadAllBytes(inputDllPath);

        var cft = new ControlFlowTransform(seed);
        bytes = cft.Transform(bytes);

        var mmt = new MetadataManglingTransform(seed);
        bytes = mmt.Transform(bytes);

        File.WriteAllBytes(inputDllPath, bytes);

        if (mapPath is not null)
        {
            var map = File.Exists(mapPath)
                ? DeobfuscationMap.LoadFromFile(mapPath)
                : new DeobfuscationMap();
            map.MetadataRenames = mmt.GetRenameMappings();
            map.ControlFlowApplied = true;
            map.SaveToFile(mapPath);
        }
    }
}
