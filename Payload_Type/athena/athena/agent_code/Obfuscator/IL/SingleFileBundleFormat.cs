using System.IO.Compression;
using System.Text;

namespace Obfuscator.IL;

internal sealed record BundleEntry(
    long Offset,
    long Size,
    long CompressedSize,
    byte FileType,
    string RelativePath)
{
    public bool IsCompressed => CompressedSize != 0;

    public bool IsRootFile =>
        !RelativePath.Contains('/') && !RelativePath.Contains('\\');

    public bool IsDll =>
        RelativePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    public bool IsDepsJson =>
        RelativePath.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase);

    public bool IsRuntimeConfigJson =>
        RelativePath.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase);

    public bool IsStageable =>
        IsRootFile && (IsDll || IsDepsJson || IsRuntimeConfigJson);
}

internal sealed record BundleManifest(
    uint Version,
    string BundleId,
    ulong Flags,
    IReadOnlyList<BundleEntry> Entries);

internal sealed record BundleRepackContext(
    byte[] OriginalExe,
    BundleManifest Manifest,
    string StagedDir,
    IReadOnlyDictionary<string, string> AssemblyRenames);

/// <summary>
/// Parses and reconstructs .NET 6+ single-file apphost bundles.
/// </summary>
internal static class SingleFileBundleFormat
{
    private const int SignatureOffsetBytes = 8;
    private const uint MinDepsHeaderVersion = 2;
    private const uint MinCompressionVersion = 6;
    private const byte AssemblyFileType = 1;
    private const long AssemblyAlignment = 16L;

    private static readonly byte[] BundleSignature =
    [
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
        0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
        0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae,
    ];

    public static bool IsBundle(byte[] fileBytes)
    {
        int sigPos = FindSignature(fileBytes);
        if (sigPos < SignatureOffsetBytes)
            return false;

        long headerOffset = BitConverter.ToInt64(fileBytes, sigPos - SignatureOffsetBytes);
        return headerOffset > 0 && headerOffset < fileBytes.LongLength;
    }

    public static int FindSignature(byte[] data) =>
        data.AsSpan().IndexOf(BundleSignature);

    public static BundleManifest ReadManifest(byte[] bundleBytes)
    {
        int sigPos = FindSignature(bundleBytes);
        if (sigPos < SignatureOffsetBytes)
            throw new InvalidDataException("Single-file bundle signature not found.");

        long headerOffset = BitConverter.ToInt64(bundleBytes, sigPos - SignatureOffsetBytes);
        if (headerOffset <= 0 || headerOffset >= bundleBytes.LongLength)
            throw new InvalidDataException("Single-file bundle header offset is out of range.");

        using var stream = new MemoryStream(
            bundleBytes, (int)headerOffset, bundleBytes.Length - (int)headerOffset);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        return ReadManifestFromReader(reader);
    }

    public static byte[] ExtractEntryBytes(byte[] bundleBytes, BundleEntry entry)
    {
        if (!entry.IsCompressed)
            return bundleBytes[(int)entry.Offset .. (int)(entry.Offset + entry.Size)];

        var stored = bundleBytes[(int)entry.Offset .. (int)(entry.Offset + entry.CompressedSize)];
        return Decompress(stored).Bytes;
    }

    public static byte[] RebuildBundle(BundleRepackContext context)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        long appHostLength = context.Manifest.Entries.Count > 0
            ? context.Manifest.Entries.Min(entry => entry.Offset)
            : context.OriginalExe.LongLength;
        stream.Write(context.OriginalExe, 0, (int)appHostLength);

        var newEntries = WriteEntries(stream, context);
        long manifestOffset = stream.Position;
        WriteManifest(writer, context.Manifest, newEntries);
        writer.Flush();

        return PatchManifestOffset(stream.ToArray(), manifestOffset);
    }

    private static BundleManifest ReadManifestFromReader(BinaryReader reader)
    {
        uint version = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        int fileCount = reader.ReadInt32();
        string bundleId = reader.ReadString();
        ulong flags = version >= MinDepsHeaderVersion ? ReadV2HeaderFlags(reader) : 0UL;
        var entries = new List<BundleEntry>(fileCount);
        for (int index = 0; index < fileCount; index++)
            entries.Add(ReadEntry(reader, version));
        return new BundleManifest(version, bundleId, flags, entries);
    }

    private static ulong ReadV2HeaderFlags(BinaryReader reader)
    {
        _ = reader.ReadInt64();
        _ = reader.ReadInt64();
        _ = reader.ReadInt64();
        _ = reader.ReadInt64();
        return reader.ReadUInt64();
    }

    private static BundleEntry ReadEntry(BinaryReader reader, uint version)
    {
        long offset = reader.ReadInt64();
        long size = reader.ReadInt64();
        long compressedSize = version >= MinCompressionVersion ? reader.ReadInt64() : 0L;
        byte fileType = reader.ReadByte();
        string relativePath = reader.ReadString();
        return new BundleEntry(offset, size, compressedSize, fileType, relativePath);
    }

    private static List<BundleEntry> WriteEntries(
        MemoryStream stream,
        BundleRepackContext context)
    {
        var written = new List<BundleEntry>(context.Manifest.Entries.Count);
        foreach (var entry in context.Manifest.Entries.OrderBy(item => item.Offset))
            written.Add(WriteSingleEntry(stream, entry, context));
        return written;
    }

    private static BundleEntry WriteSingleEntry(
        MemoryStream stream,
        BundleEntry original,
        BundleRepackContext context)
    {
        if (original.FileType == AssemblyFileType)
            AlignStream(stream, AssemblyAlignment);

        long offset = stream.Position;
        var (storedBytes, rawSize, compressedSize, relativePath) =
            ResolveEntryPayload(original, context);
        stream.Write(storedBytes, 0, storedBytes.Length);
        return new BundleEntry(
            offset, rawSize, compressedSize, original.FileType, relativePath);
    }

    private static void AlignStream(MemoryStream stream, long alignment)
    {
        long remainder = stream.Position % alignment;
        if (remainder == 0)
            return;

        stream.Write(new byte[alignment - remainder]);
    }

    private static (byte[] Stored, long Size, long CompressedSize, string Path)
        ResolveEntryPayload(BundleEntry original, BundleRepackContext context)
    {
        string relativePath = ResolveRelativePath(original, context.AssemblyRenames);
        string stagedPath = Path.Combine(context.StagedDir, relativePath);
        if (!original.IsStageable || !File.Exists(stagedPath))
            return CopyOriginalPayload(original, context.OriginalExe);

        byte[] rawBytes = File.ReadAllBytes(stagedPath);
        if (!original.IsCompressed)
            return (rawBytes, rawBytes.LongLength, 0L, relativePath);

        var origStored = GetOriginalStoredSlice(original, context.OriginalExe);
        bool useBrotli = Decompress(origStored).WasBrotli;
        byte[] compressed = Compress(rawBytes, useBrotli);
        return (compressed, rawBytes.LongLength, compressed.LongLength, relativePath);
    }

    private static string ResolveRelativePath(
        BundleEntry original,
        IReadOnlyDictionary<string, string> renames)
    {
        if (!original.IsRootFile || !original.IsDll)
            return original.RelativePath;

        string baseName = Path.GetFileNameWithoutExtension(original.RelativePath);
        return renames.TryGetValue(baseName, out var renamed)
            ? renamed + ".dll"
            : original.RelativePath;
    }

    private static (byte[] Stored, long Size, long CompressedSize, string Path)
        CopyOriginalPayload(BundleEntry original, byte[] originalExe)
    {
        byte[] stored = GetOriginalStoredSlice(original, originalExe);
        return (stored, original.Size, original.CompressedSize, original.RelativePath);
    }

    private static byte[] GetOriginalStoredSlice(BundleEntry original, byte[] originalExe)
    {
        long length = original.IsCompressed ? original.CompressedSize : original.Size;
        return originalExe[(int)original.Offset .. (int)(original.Offset + length)];
    }

    private static void WriteManifest(
        BinaryWriter writer,
        BundleManifest manifest,
        IReadOnlyList<BundleEntry> entries)
    {
        writer.Write(manifest.Version);
        writer.Write(0u);
        writer.Write(entries.Count);
        writer.Write(manifest.BundleId);
        if (manifest.Version >= MinDepsHeaderVersion)
            WriteV2Offsets(writer, entries, manifest.Flags);

        foreach (var entry in entries)
            WriteManifestEntry(writer, entry, manifest.Version);
    }

    private static void WriteV2Offsets(
        BinaryWriter writer,
        IReadOnlyList<BundleEntry> entries,
        ulong flags)
    {
        var deps = entries.FirstOrDefault(entry => entry.IsDepsJson);
        var rcfg = entries.FirstOrDefault(entry => entry.IsRuntimeConfigJson);
        writer.Write(deps?.Offset ?? 0L);
        writer.Write(deps?.Size ?? 0L);
        writer.Write(rcfg?.Offset ?? 0L);
        writer.Write(rcfg?.Size ?? 0L);
        writer.Write(flags);
    }

    private static void WriteManifestEntry(
        BinaryWriter writer,
        BundleEntry entry,
        uint version)
    {
        writer.Write(entry.Offset);
        writer.Write(entry.Size);
        if (version >= MinCompressionVersion)
            writer.Write(entry.CompressedSize);
        writer.Write(entry.FileType);
        writer.Write(entry.RelativePath);
    }

    private static byte[] PatchManifestOffset(byte[] bundleBytes, long manifestOffset)
    {
        int sigPos = FindSignature(bundleBytes);
        if (sigPos >= SignatureOffsetBytes)
        {
            var offsetBytes = BitConverter.GetBytes(manifestOffset);
            Array.Copy(offsetBytes, 0, bundleBytes, sigPos - SignatureOffsetBytes, SignatureOffsetBytes);
        }
        return bundleBytes;
    }

    private static (byte[] Bytes, bool WasBrotli) Decompress(byte[] compressed)
    {
        if (TryDecompressDeflate(compressed, out var deflateBytes))
            return (deflateBytes, false);

        using var input = new MemoryStream(compressed);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return (output.ToArray(), true);
    }

    private static bool TryDecompressDeflate(byte[] compressed, out byte[] bytes)
    {
        try
        {
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            bytes = output.ToArray();
            return true;
        }
        catch (InvalidDataException)
        {
            bytes = [];
            return false;
        }
    }

    private static byte[] Compress(byte[] rawBytes, bool useBrotli)
    {
        using var output = new MemoryStream();
        using Stream compressor = useBrotli
            ? new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true)
            : new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true);
        compressor.Write(rawBytes, 0, rawBytes.Length);
        compressor.Dispose();
        return output.ToArray();
    }
}
