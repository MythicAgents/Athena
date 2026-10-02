using System.IO.Compression;
using System.Text;

namespace AssemblyNameObfuscator;

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
        var unixMode = OperatingSystem.IsWindows()
            ? (UnixFileMode?)null
            : File.GetUnixFileMode(inputExe);
        var exeBytes = File.ReadAllBytes(inputExe);

        int sigPos = FindSignature(exeBytes);
        if (sigPos < 8)
            throw new InvalidOperationException(
                $"[patch-bundle] Bundle signature not found in '{inputExe}'. "
                + "Is this a single-file self-contained bundle?");

        long headerOffset = BitConverter.ToInt64(exeBytes, sigPos - 8);
        var (entries, bundleVersion, bundleId, headerFlags) =
            ParseManifest(exeBytes, headerOffset);

        var tempDir = Path.Combine(
            Path.GetTempPath(), "obf_bundle_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            ExtractDllEntries(exeBytes, entries, tempDir);

            var entryAssemblyName = FindEntryAssemblyName(entries);
            var transform = new AssemblyIdentityRenamer(_seed);
            var renameMap = transform.RenameAll(
                tempDir, skipFileRename: false,
                extraSkipNames: entryAssemblyName is not null
                    ? [entryAssemblyName] : null);

            long minOffset = entries.Count > 0
                ? entries.Min(e => e.Offset)
                : exeBytes.LongLength;
            var appHostBytes = exeBytes[..(int)minOffset];

            var result = BuildNewBundle(
                appHostBytes, exeBytes, entries, tempDir,
                renameMap, bundleVersion, bundleId, headerFlags);

            WritePatchedBundle(inputExe, result, unixMode);
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

    private static void ExtractDllEntries(
        byte[] exeBytes,
        IReadOnlyList<BundleEntry> entries,
        string tempDir)
    {
        foreach (var entry in entries.Where(e => e.IsDll))
        {
            var dllBytes = entry.IsCompressed
                ? Decompress(GetStoredSlice(exeBytes, entry.Offset, entry.CompressedSize)).Bytes
                : GetStoredSlice(exeBytes, entry.Offset, entry.Size);
            var extractedPath = Path.Combine(
                tempDir,
                entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(extractedPath)!);
            File.WriteAllBytes(extractedPath, dllBytes);
        }
    }

    private static void WritePatchedBundle(
        string inputExe,
        byte[] result,
        UnixFileMode? unixMode)
    {
        var tempOutput = inputExe + ".obf_tmp";
        File.WriteAllBytes(tempOutput, result);
        File.Move(tempOutput, inputExe, overwrite: true);
        if (unixMode.HasValue && !OperatingSystem.IsWindows())
            File.SetUnixFileMode(inputExe, unixMode.Value);
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
        ulong flags      = version >= 2 ? ReadV2HeaderFlags(br) : 0UL;

        var entries = new List<BundleEntry>(fileCount);
        for (int i = 0; i < fileCount; i++)
            entries.Add(ReadManifestEntry(br, version));

        return (entries, version, bundleId, flags);
    }

    private static ulong ReadV2HeaderFlags(BinaryReader br)
    {
        _ = br.ReadInt64(); // DepsJsonOffset
        _ = br.ReadInt64(); // DepsJsonSize
        _ = br.ReadInt64(); // RuntimeConfigJsonOffset
        _ = br.ReadInt64(); // RuntimeConfigJsonSize
        return br.ReadUInt64();
    }

    private static BundleEntry ReadManifestEntry(BinaryReader br, uint version)
    {
        long   offset         = br.ReadInt64();
        long   size           = br.ReadInt64();
        long   compressedSize = version >= 6 ? br.ReadInt64() : 0L;
        byte   type           = br.ReadByte();
        string relativePath   = br.ReadString();
        return new BundleEntry(offset, size, compressedSize, type, relativePath);
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
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        ms.Write(appHostBytes, 0, appHostBytes.Length);
        var newEntries = WriteEmbeddedEntries(ms, originalExe, entries, tempDir, renameMap);

        long manifestOffset = ms.Position;
        WriteManifest(bw, newEntries, bundleVersion, bundleId, headerFlags);
        bw.Flush();

        return PatchManifestOffset(ms.ToArray(), manifestOffset);
    }

    private static List<NewEntry> WriteEmbeddedEntries(
        MemoryStream ms,
        byte[] originalExe,
        List<BundleEntry> entries,
        string tempDir,
        Dictionary<string, string> renameMap)
    {
        const long AssemblyAlignment = 16L;
        var newEntries = new List<NewEntry>(entries.Count);

        foreach (var orig in entries.OrderBy(e => e.Offset))
        {
            if (orig.FileType == 1 /* Assembly */)
                AlignStream(ms, AssemblyAlignment);

            long fileOffset = ms.Position;
            var (fileBytes, newSize, newCompressedSize, newRelPath) =
                ResolveEntryPayload(orig, originalExe, tempDir, renameMap);

            ms.Write(fileBytes, 0, fileBytes.Length);
            newEntries.Add(new NewEntry(
                fileOffset, newSize, newCompressedSize, orig.FileType, newRelPath));
        }

        return newEntries;
    }

    private static void AlignStream(MemoryStream ms, long alignment)
    {
        long rem = ms.Position % alignment;
        if (rem != 0)
            ms.Write(new byte[alignment - rem]);
    }

    private static (byte[] Bytes, long Size, long CompressedSize, string RelativePath)
        ResolveEntryPayload(
            BundleEntry orig,
            byte[] originalExe,
            string tempDir,
            Dictionary<string, string> renameMap)
    {
        if (!orig.IsDll)
            return CopyOriginalPayload(orig, originalExe);

        string origBase = Path.GetFileNameWithoutExtension(orig.RelativePath);
        string newBase  = renameMap.TryGetValue(origBase, out var nb) ? nb : origBase;
        string? relativeDirectory = Path.GetDirectoryName(
            orig.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        string newRelPath = string.IsNullOrEmpty(relativeDirectory)
            ? newBase + ".dll"
            : Path.Combine(relativeDirectory, newBase + ".dll")
                .Replace(Path.DirectorySeparatorChar, '/');

        var renamedFile = Path.Combine(
            tempDir, relativeDirectory ?? string.Empty, newBase + ".dll");
        var originalFile = Path.Combine(
            tempDir, relativeDirectory ?? string.Empty, origBase + ".dll");
        var candidatePath = File.Exists(renamedFile)
            ? renamedFile
            : File.Exists(originalFile) ? originalFile : null;
        if (candidatePath is null)
            return CopyOriginalPayload(orig, originalExe, newRelPath);

        byte[] rawBytes = File.ReadAllBytes(candidatePath);
        if (!orig.IsCompressed)
            return (rawBytes, rawBytes.Length, 0L, newRelPath);

        var origStored = GetStoredSlice(originalExe, orig.Offset, orig.CompressedSize);
        (_, bool wasBrotli) = Decompress(origStored);
        byte[] compressed = Compress(rawBytes, wasBrotli);
        return (compressed, rawBytes.Length, compressed.Length, newRelPath);
    }

    private static (byte[] Bytes, long Size, long CompressedSize, string RelativePath)
        CopyOriginalPayload(BundleEntry orig, byte[] originalExe, string? relativePath = null)
    {
        long stored = orig.IsCompressed ? orig.CompressedSize : orig.Size;
        byte[] fileBytes = GetStoredSlice(originalExe, orig.Offset, stored);
        return (fileBytes, orig.Size, orig.CompressedSize, relativePath ?? orig.RelativePath);
    }

    private static byte[] GetStoredSlice(byte[] data, long offset, long length) =>
        data[(int)offset .. (int)(offset + length)];

    private static void WriteManifest(
        BinaryWriter bw,
        List<NewEntry> newEntries,
        uint bundleVersion,
        string bundleId,
        ulong headerFlags)
    {
        bw.Write(bundleVersion); // MajorVersion
        bw.Write(0u);            // MinorVersion
        bw.Write(newEntries.Count);
        bw.Write(bundleId);      // reuse original BundleID

        if (bundleVersion >= 2)
        {
            var deps = newEntries.FirstOrDefault(e =>
                e.RelativePath.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase));
            var rcfg = newEntries.FirstOrDefault(e =>
                e.RelativePath.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase));

            bw.Write(deps?.Offset ?? 0L);
            bw.Write(deps?.Size   ?? 0L);
            bw.Write(rcfg?.Offset ?? 0L);
            bw.Write(rcfg?.Size   ?? 0L);
            bw.Write(headerFlags);
        }

        foreach (var ne in newEntries)
        {
            bw.Write(ne.Offset);
            bw.Write(ne.Size);
            if (bundleVersion >= 6)
                bw.Write(ne.CompressedSize);
            bw.Write(ne.FileType);
            bw.Write(ne.RelativePath);
        }
    }

    private static byte[] PatchManifestOffset(byte[] result, long manifestOffset)
    {
        int newSigPos = FindSignature(result);
        if (newSigPos >= 8)
        {
            var offsetBytes = BitConverter.GetBytes(manifestOffset);
            Array.Copy(offsetBytes, 0, result, newSigPos - 8, 8);
        }
        return result;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static (byte[] Bytes, bool WasBrotli) Decompress(byte[] compressed)
    {
        try
        {
            using var csIn  = new MemoryStream(compressed);
            using var ds    = new DeflateStream(csIn, CompressionMode.Decompress);
            using var csOut = new MemoryStream();
            ds.CopyTo(csOut);
            return (csOut.ToArray(), false);
        }
        catch (InvalidDataException) { }

        using var csIn2  = new MemoryStream(compressed);
        using var bs     = new BrotliStream(csIn2, CompressionMode.Decompress);
        using var csOut2 = new MemoryStream();
        bs.CopyTo(csOut2);
        return (csOut2.ToArray(), true);
    }

    private static byte[] Compress(byte[] data, bool useBrotli)
    {
        using var csOut = new MemoryStream();
        using (Stream compressor = useBrotli
            ? new BrotliStream(csOut, CompressionLevel.Optimal, leaveOpen: true)
            : new DeflateStream(csOut, CompressionLevel.Optimal, leaveOpen: true))
        {
            compressor.Write(data, 0, data.Length);
        }
        return csOut.ToArray();
    }

    private static int FindSignature(byte[] data) =>
        data.AsSpan().IndexOf(BundleSignature);

    private static string? FindEntryAssemblyName(IEnumerable<BundleEntry> entries)
    {
        var depsEntry = entries.FirstOrDefault(e =>
            e.RelativePath.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase));
        return depsEntry is null
            ? null
            : Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(depsEntry.RelativePath));
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

    private sealed record NewEntry(
        long Offset, long Size, long CompressedSize,
        byte FileType, string RelativePath);
}
