using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Agent.Utilities;

public static class AssemblyIdentity
{
    private const string Chars =
        "abcdefghijklmnopqrstuvwxyz0123456789"
        + "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static IEnumerable<string> GetLoadCandidates(
        string agentUuid,
        string logicalName)
    {
        yield return GetObfuscatedName(agentUuid, logicalName);
        yield return logicalName;
    }

    public static string GetObfuscatedName(string agentUuid, string logicalName)
    {
        if (string.IsNullOrWhiteSpace(agentUuid))
            throw new ArgumentException("Agent UUID is required", nameof(agentUuid));
        if (string.IsNullOrWhiteSpace(logicalName))
            throw new ArgumentException("Logical assembly name is required", nameof(logicalName));

        var uuidHash = SHA256.HashData(Encoding.UTF8.GetBytes(agentUuid));
        var seed = BinaryPrimitives.ReadInt32BigEndian(uuidHash.AsSpan(28, 4))
            & int.MaxValue;
        var nameHash = SHA256.HashData(
            Encoding.UTF8.GetBytes($"{seed}:{logicalName}"));
        var result = new StringBuilder("_");
        for (var i = 0; i < 5; i++)
            result.Append(Chars[nameHash[i] % Chars.Length]);
        return result.ToString();
    }
}
