using System.Text.Json;
using System.Text.Json.Nodes;

namespace Obfuscator.IL;

public static class DepsJsonPatcher
{
    public static void Patch(
        string depsJsonPath,
        IReadOnlyDictionary<string, string> assemblyRenameMap)
    {
        var rendered = Render(File.ReadAllBytes(depsJsonPath), assemblyRenameMap);
        AtomicWrite(depsJsonPath, rendered);
    }

    public static byte[] Render(
        byte[] originalBytes,
        IReadOnlyDictionary<string, string> assemblyRenameMap)
    {
        ArgumentNullException.ThrowIfNull(originalBytes);
        ArgumentNullException.ThrowIfNull(assemblyRenameMap);
        var renames = assemblyRenameMap
            .Where(pair => !string.Equals(pair.Key, pair.Value, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (renames.Count == 0)
            return originalBytes.ToArray();

        var duplicateDestination = renames
            .GroupBy(pair => pair.Value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateDestination is not null)
            throw new InvalidDataException(
                $"Multiple assemblies would be renamed to '{duplicateDestination.Key}'.");

        var root = JsonNode.Parse(originalBytes)?.AsObject()
            ?? throw new JsonException("The deps manifest root must be a JSON object.");

        if (root["targets"] is JsonObject targets)
        {
            foreach (var targetNode in targets.Select(pair => pair.Value).OfType<JsonObject>())
            {
                RenameLibraryIds(targetNode, renames);
                foreach (var library in targetNode.Select(pair => pair.Value).OfType<JsonObject>())
                {
                    if (library["dependencies"] is JsonObject dependencies)
                        RenameExactProperties(dependencies, renames);
                    if (library["runtime"] is JsonObject runtime)
                        RenameRuntimeAssets(runtime, renames);
                    if (library["runtimeTargets"] is JsonObject runtimeTargets)
                        RenameRuntimeTargets(runtimeTargets, renames);
                }
            }
        }

        if (root["libraries"] is JsonObject libraries)
            RenameLibraryIds(libraries, renames);

        var rendered = root.ToJsonString(
            new JsonSerializerOptions { WriteIndented = true });
        _ = JsonNode.Parse(rendered)
            ?? throw new JsonException("Rendered deps manifest was empty.");
        return System.Text.Encoding.UTF8.GetBytes(rendered);
    }

    private static void RenameLibraryIds(
        JsonObject libraries,
        IReadOnlyDictionary<string, string> renames)
    {
        RenameProperties(libraries, key =>
        {
            var slash = key.IndexOf('/');
            if (slash <= 0 || !renames.TryGetValue(key[..slash], out var renamed))
                return key;
            return renamed + key[slash..];
        });
    }

    private static void RenameExactProperties(
        JsonObject properties,
        IReadOnlyDictionary<string, string> renames) =>
        RenameProperties(properties, key => renames.TryGetValue(key, out var renamed) ? renamed : key);

    private static void RenameRuntimeAssets(
        JsonObject assets,
        IReadOnlyDictionary<string, string> renames) =>
        RenameProperties(assets, key => RenameManagedAsset(key, renames));

    private static void RenameRuntimeTargets(
        JsonObject assets,
        IReadOnlyDictionary<string, string> renames) =>
        RenameProperties(assets, key =>
        {
            if (assets[key] is not JsonObject metadata
                || metadata["assetType"]?.GetValue<string>() != "runtime")
                return key;
            return RenameManagedAsset(key, renames);
        });

    private static string RenameManagedAsset(
        string assetPath,
        IReadOnlyDictionary<string, string> renames)
    {
        var separator = Math.Max(assetPath.LastIndexOf('/'), assetPath.LastIndexOf('\\'));
        var fileName = assetPath[(separator + 1)..];
        if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return assetPath;
        var assemblyName = fileName[..^4];
        if (!renames.TryGetValue(assemblyName, out var renamed))
            return assetPath;
        return assetPath[..(separator + 1)] + renamed + fileName[^4..];
    }

    private static void RenameProperties(
        JsonObject obj,
        Func<string, string> getDestination)
    {
        var moves = obj.Select(pair => pair.Key)
            .Select(source => (Source: source, Destination: getDestination(source)))
            .Where(move => move.Source != move.Destination)
            .ToArray();
        var destinations = obj.Select(pair => pair.Key)
            .Except(moves.Select(move => move.Source), StringComparer.Ordinal)
            .Concat(moves.Select(move => move.Destination))
            .ToArray();
        var collision = destinations
            .GroupBy(key => key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (collision is not null)
            throw new InvalidDataException(
                $"Deps manifest rewrite would create duplicate property '{collision.Key}'.");

        foreach (var (source, destination) in moves)
        {
            var value = obj[source];
            obj.Remove(source);
            obj[destination] = value;
        }
    }

    private static void AtomicWrite(string path, byte[] bytes)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
