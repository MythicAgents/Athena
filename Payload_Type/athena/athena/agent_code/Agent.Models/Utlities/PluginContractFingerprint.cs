using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Agent.Utilities;

public static class PluginContractFingerprint
{
    public const string MetadataKey = "AthenaPluginContract";
    private const string Domain = "athena-contract-v1";

    public static string Derive(string payloadUuid)
    {
        if (!Guid.TryParse(payloadUuid, out var parsed))
            throw new ArgumentException("Payload UUID must be a valid UUID.", nameof(payloadUuid));

        string normalized = parsed.ToString("D").ToLowerInvariant();
        byte[] digest = SHA256.HashData(
            Encoding.UTF8.GetBytes($"{Domain}:{normalized}"));
        return Convert.ToHexStringLower(digest);
    }

    public static void Validate(
        Assembly assembly,
        string payloadUuid,
        bool fingerprintRequired)
    {
        string[] fingerprints = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key == MetadataKey)
            .Select(attribute => attribute.Value ?? string.Empty)
            .ToArray();

        if (fingerprints.Length == 0 && !fingerprintRequired)
            return;

        string expected = Derive(payloadUuid);
        if (fingerprints.Length != 1 ||
            !string.Equals(fingerprints[0], expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Plugin contract mismatch: the plugin was built for a different payload contract.");
        }
    }
}