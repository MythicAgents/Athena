using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Obfuscator;

/// <summary>
/// Accumulates rename and transform info from both the source and IL
/// obfuscation stages and serializes it to a JSON file. Both stages
/// can write to the same file: source stage writes first, IL stage merges.
/// </summary>
public sealed class DeobfuscationMap
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int? Seed { get; set; }
    public string? Uuid { get; set; }

    // Source-level info
    public HelperInfo? StringDecryptor { get; set; }
    public HelperInfo? IndirectCaller { get; set; }
    public Dictionary<string, string>? UuidRenames { get; set; }

    // IL-level info
    public Dictionary<string, string>? MetadataRenames { get; set; }
    public bool ControlFlowApplied { get; set; }

    public record HelperInfo(string Namespace, string ClassName, string MethodName);

    /// <summary>
    /// Writes the map to disk. If the file already exists, loads it first
    /// and merges non-null values from this instance over the existing data.
    /// </summary>
    public void SaveToFile(string path)
    {
        DeobfuscationMap target;
        if (File.Exists(path))
        {
            target = LoadFromFile(path);
            MergeInto(target);
        }
        else
        {
            target = this;
        }

        var json = JsonSerializer.Serialize(target, JsonOptions);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    /// <summary>
    /// Loads an existing map file from disk.
    /// </summary>
    public static DeobfuscationMap LoadFromFile(string path)
    {
        var json = File.ReadAllText(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<DeobfuscationMap>(json, JsonOptions)
            ?? new DeobfuscationMap();
    }

    /// <summary>
    /// Merges non-default values from this instance into <paramref name="target"/>.
    /// </summary>
    private void MergeInto(DeobfuscationMap target)
    {
        if (Seed.HasValue) target.Seed = Seed;
        if (Uuid is not null) target.Uuid = Uuid;
        if (StringDecryptor is not null) target.StringDecryptor = StringDecryptor;
        if (IndirectCaller is not null) target.IndirectCaller = IndirectCaller;
        if (UuidRenames is not null) target.UuidRenames = UuidRenames;
        if (MetadataRenames is not null) target.MetadataRenames = MetadataRenames;
        if (ControlFlowApplied) target.ControlFlowApplied = true;
    }
}
