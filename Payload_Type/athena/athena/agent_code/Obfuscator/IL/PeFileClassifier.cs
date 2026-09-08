using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Obfuscator.IL;

internal enum PeFileKind
{
    Managed,
    Native,
}

internal static class PeFileClassifier
{
    public static PeFileKind Classify(byte[] bytes, string path)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new PEReader(stream);
            var headers = reader.PEHeaders;
            if (headers.PEHeader is null)
                throw new BadImageFormatException("The PE optional header is missing.");

            if (headers.CorHeader is null)
                return PeFileKind.Native;

            if (headers.CorHeader.MetadataDirectory.Size <= 0)
                throw new BadImageFormatException("The CLR metadata directory is missing.");

            _ = reader.GetMetadataReader();
            return PeFileKind.Managed;
        }
        catch (BadImageFormatException ex)
        {
            throw InvalidImage(path, ex);
        }
        catch (IOException ex)
        {
            throw InvalidImage(path, ex);
        }
    }

    public static InvalidDataException InvalidImage(string path, Exception inner) =>
        new($"Invalid PE image '{Path.GetFileName(path)}': {inner.Message}", inner);
}
