using System.Text;
using Obfuscator.IL.Transforms;

namespace Obfuscator.IL;

/// <summary>
/// Patches embedded assembly names in a .NET 6+ single-file bundle exe.
/// Implements the bundle format natively — no dependency on
/// Microsoft.NET.HostModel (the NuGet package's Extractor only understands
/// .NET 3.1 bundles; the SDK build dropped Extractor entirely).
/// </summary>
public sealed class BundlePatcher
{
    // 32-byte magic stored in every .NET apphost binary.
    // = BundleHeaderPlaceholder[8..40] from Microsoft.NET.HostModel.Bundle.Bundler
    private static readonly byte[] BundleSignature =
    [
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
        0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
        0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae
    ];

    private readonly int _seed;

    public BundlePatcher(int seed) => _seed = seed;

    /// <summary>
    /// Patches the given single-file bundle exe in-place: extracts embedded
    /// assemblies, renames their PE identities, and rebuilds the bundle.
    /// </summary>
    /// <param name="inputExe">Absolute path to the self-contained single-file exe.</param>
    /// <param name="mapPath">Optional path to write/merge a <see cref="DeobfuscationMap"/>.</param>
    /// <returns>The rename map: original assembly name → obfuscated name.</returns>
    public Dictionary<string, string> Patch(string inputExe, string? mapPath = null)
    {
        var exeBytes = File.ReadAllBytes(inputExe);

        // Locate the 32-byte signature embedded somewhere in the apphost binary.
        int sigPos = FindSignature(exeBytes);
        if (sigPos < 8)
            throw new InvalidOperationException(
                $"[patch-bundle] Bundle signature not found in '{inputExe}'. "
                + "Is this a single-file self-contained bundle?");

        // The 8 bytes immediately before the signature hold the manifest offset.
        long headerOffset = BitConverter.ToInt64(exeBytes, sigPos - 8);
        var (entries, bundleVersion, bundleId, headerFlags) =
            ParseManifest(exeBytes, headerOffset);

        // Extract uncompressed DLLs to a temp dir for renaming.
        var tempDir = Path.Combine(
            Path.GetTempPath(), "obf_bundle_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            foreach (var e in entries)
            {
                if (!e.IsDll || e.IsCompressed) continue;
                File.WriteAllBytes(
                    Path.Combine(tempDir, Path.GetFileName(e.RelativePath)),
                    exeBytes[(int)e.Offset .. (int)(e.Offset + e.Size)]);
            }

            var entryAssemblyName = FindEntryAssemblyName(entries);
            var transform = new AssemblyRenameTransform(_seed);
            var renameMap = transform.RenameAll(
                tempDir, skipFileRename: false,
                extraSkipNames: entryAssemblyName is not null
                    ? [entryAssemblyName] : null);

            // Determine the apphost prefix (everything before the first embedded file).
            long minOffset = entries.Count > 0
                ? entries.Min(e => e.Offset)
                : exeBytes.LongLength;
            var appHostBytes = exeBytes[..(int)minOffset];

            var result = BuildNewBundle(
                appHostBytes, exeBytes, entries, tempDir,
                renameMap, bundleVersion, bundleId, headerFlags);

            // Atomic replace.
            var tempOutput = inputExe + ".obf_tmp";
            File.WriteAllBytes(tempOutput, result);
            File.Move(tempOutput, inputExe, overwrite: true);

            if (mapPath is not null)
            {
                var map = File.Exists(mapPath)
                    ? DeobfuscationMap.LoadFromFile(mapPath)
                    : new DeobfuscationMap();
                var existing = map.MetadataRenames ?? [];
                foreach (var (k, v) in renameMap)
                    existing.TryAdd("asm:" + k, v);
                map.MetadataRenames = existing;
                map.SaveToFile(mapPath);
            }

            Console.WriteLine(
                $"[patch-bundle] Renamed {renameMap.Count} assemblies. "
                + $"Entry '{entryAssemblyName}' preserved.");
            return renameMap;
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    // ─── Bundle format parsing ──────────────────────────────────────────────

    private static (List<BundleEntry> Entries, uint Version, string BundleId,
        ulong Flags) ParseManifest(byte[] data, long headerOffset)
    {
        using var br = new BinaryReader(
            new MemoryStream(data, (int)headerOffset,
                data.Length - (int)headerOffset),
            Encoding.UTF8);

        uint version     = br.ReadUInt32();
        _                = br.ReadUInt32(); // MinorVersion (always 0)
        int fileCount    = br.ReadInt32();
        string bundleId  = br.ReadString(); // LEB128-prefixed UTF-8

        ulong flags = 0;
        if (version >= 2)
        {
            _ = br.ReadInt64(); // DepsJsonOffset
            _ = br.ReadInt64(); // DepsJsonSize
            _ = br.ReadInt64(); // RuntimeConfigJsonOffset
            _ = br.ReadInt64(); // RuntimeConfigJsonSize
            flags = br.ReadUInt64();
        }

        var entries = new List<BundleEntry>(fileCount);
        for (int i = 0; i < fileCount; i++)
        {
            long   offset         = br.ReadInt64();
            long   size           = br.ReadInt64();
            long   compressedSize = version >= 6 ? br.ReadInt64() : 0L;
            byte   type           = br.ReadByte();
            string relativePath   = br.ReadString(); // LEB128-prefixed UTF-8
            entries.Add(new BundleEntry(offset, size, compressedSize, type, relativePath));
        }

        return (entries, version, bundleId, flags);
    }

    // ─── Bundle rebuilding ──────────────────────────────────────────────────

    private static byte[] BuildNewBundle(
        byte[] appHostBytes,
        byte[] originalExe,
        List<BundleEntry> entries,
        string tempDir,
        Dictionary<string, string> renameMap,
        uint   bundleVersion,
        string bundleId,
        ulong  headerFlags)
    {
        // Assembly alignment for win-x64 / linux-x64 bundles is 16 bytes.
        const long AssemblyAlignment = 16L;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        // ── Apphost prefix ─────────────────────────────────────────────────
        ms.Write(appHostBytes, 0, appHostBytes.Length);

        // ── Embedded files (in original offset order) ──────────────────────
        var newEntries = new List<NewEntry>(entries.Count);
        foreach (var orig in entries.OrderBy(e => e.Offset))
        {
            // Pad assemblies to the required alignment boundary.
            if (orig.FileType == 1 /* Assembly */)
            {
                long rem = ms.Position % AssemblyAlignment;
                if (rem != 0)
                {
                    var pad = new byte[AssemblyAlignment - rem];
                    ms.Write(pad, 0, pad.Length);
                }
            }

            long fileOffset    = ms.Position;
            string newRelPath  = orig.RelativePath;
            byte[] fileBytes;

            if (orig.IsDll && !orig.IsCompressed)
            {
                // Look up the (possibly renamed) file in tempDir.
                string origBase = Path.GetFileNameWithoutExtension(orig.RelativePath);
                string newBase  = renameMap.TryGetValue(origBase, out var nb) ? nb : origBase;
                newRelPath      = newBase + ".dll";

                var renamedFile  = Path.Combine(tempDir, newBase  + ".dll");
                var originalFile = Path.Combine(tempDir, origBase + ".dll");

                if      (File.Exists(renamedFile))  fileBytes = File.ReadAllBytes(renamedFile);
                else if (File.Exists(originalFile)) fileBytes = File.ReadAllBytes(originalFile);
                else    fileBytes = originalExe[(int)orig.Offset .. (int)(orig.Offset + orig.Size)];
            }
            else
            {
                // Non-DLL or compressed entry: copy original bytes unchanged.
                // For compressed entries, only CompressedSize bytes are stored on disk;
                // Size is the uncompressed size and must be preserved in the manifest.
                long storedBytes = orig.IsCompressed ? orig.CompressedSize : orig.Size;
                fileBytes = originalExe[(int)orig.Offset .. (int)(orig.Offset + storedBytes)];
            }

            ms.Write(fileBytes, 0, fileBytes.Length);
            newEntries.Add(new NewEntry(
                fileOffset,
                orig.IsCompressed ? orig.Size : fileBytes.Length,
                orig.CompressedSize,
                orig.FileType, newRelPath));
        }

        // ── Manifest ────────────────────────────────────────────────────────
        long manifestOffset = ms.Position;

        bw.Write(bundleVersion); // MajorVersion
        bw.Write(0u);            // MinorVersion
        bw.Write(newEntries.Count);
        bw.Write(bundleId);      // reuse original BundleID

        if (bundleVersion >= 2)
        {
            var deps  = newEntries.FirstOrDefault(
                e => e.RelativePath.EndsWith(".deps.json",
                    StringComparison.OrdinalIgnoreCase));
            var rcfg  = newEntries.FirstOrDefault(
                e => e.RelativePath.EndsWith(".runtimeconfig.json",
                    StringComparison.OrdinalIgnoreCase));

            bw.Write(deps  != null ? deps.Offset  : 0L);
            bw.Write(deps  != null ? deps.Size    : 0L);
            bw.Write(rcfg  != null ? rcfg.Offset  : 0L);
            bw.Write(rcfg  != null ? rcfg.Size    : 0L);
            bw.Write(headerFlags);
        }

        foreach (var ne in newEntries)
        {
            bw.Write(ne.Offset);
            bw.Write(ne.Size);
            if (bundleVersion >= 6) bw.Write(ne.CompressedSize);
            bw.Write(ne.FileType);
            bw.Write(ne.RelativePath);
        }
        bw.Flush();

        var result = ms.ToArray();

        // Patch the 8-byte header-offset field that lives just before the signature.
        int newSigPos = FindSignature(result);
        if (newSigPos >= 8)
        {
            var offsetBytes = BitConverter.GetBytes(manifestOffset);
            Array.Copy(offsetBytes, 0, result, newSigPos - 8, 8);
        }

        return result;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static int FindSignature(byte[] data)
    {
        int len = BundleSignature.Length;
        for (int i = 0; i <= data.Length - len; i++)
        {
            bool match = true;
            for (int j = 0; j < len; j++)
            {
                if (data[i + j] != BundleSignature[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>
    /// Reads the entry assembly name from the bundle manifest by finding the
    /// *.deps.json entry path — avoids needing to extract the JSON file to disk.
    /// </summary>
    private static string? FindEntryAssemblyName(IEnumerable<BundleEntry> entries)
    {
        var depsEntry = entries.FirstOrDefault(e =>
            e.RelativePath.EndsWith(".deps.json",
                StringComparison.OrdinalIgnoreCase));
        if (depsEntry is not null)
            return Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(depsEntry.RelativePath));
        return null;
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ─── Data types ─────────────────────────────────────────────────────────

    private sealed record BundleEntry(
        long Offset, long Size, long CompressedSize,
        byte FileType, string RelativePath)
    {
        public bool IsDll => RelativePath.EndsWith(
            ".dll", StringComparison.OrdinalIgnoreCase);
        public bool IsCompressed => CompressedSize != 0;
    }

    private sealed class NewEntry(
        long offset, long size, long compressedSize,
        byte fileType, string relativePath)
    {
        public long   Offset         { get; } = offset;
        public long   Size           { get; } = size;
        public long   CompressedSize { get; } = compressedSize;
        public byte   FileType       { get; } = fileType;
        public string RelativePath   { get; } = relativePath;
    }
}
