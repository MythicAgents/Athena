using System.Security.Cryptography;
using System.Text;

namespace Obfuscator.Config;

public sealed class UuidRenameMap
{
    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private readonly Dictionary<string, string> _map;
    private readonly HashSet<string> _alwaysRenameNames;
    private readonly HashSet<string> _interfaceMemberNames;

    private UuidRenameMap(
        Dictionary<string, string> map,
        HashSet<string> alwaysRename,
        HashSet<string> interfaceMembers)
    {
        _map = map;
        _alwaysRenameNames = alwaysRename;
        _interfaceMemberNames = interfaceMembers;
    }

    public static UuidRenameMap Derive(
        string uuid, ContractNames names)
    {
        var alwaysRename = new HashSet<string>(
            names.Interfaces
                .Concat(names.Types)
                .Concat(names.Namespaces));

        var interfaceMembers = new HashSet<string>(
            names.InterfaceMembers);

        var allNames = names.Interfaces
            .Concat(names.InterfaceMembers)
            .Concat(names.Types)
            .Concat(names.RecordParams)
            .Concat(names.Namespaces)
            .ToArray();

        var seed = ComputeSeed(uuid);
        var rng = new Random(seed);
        var used = new HashSet<string>();
        var map = new Dictionary<string, string>();

        foreach (var name in allNames)
        {
            if (map.ContainsKey(name))
                continue;
            var renamed = GenerateUniqueName(rng, used);
            map[name] = renamed;
        }

        return new UuidRenameMap(map, alwaysRename, interfaceMembers);
    }

    public string GetRenamed(string originalName)
    {
        return _map[originalName];
    }

    public List<string> GetAllRenamedValues()
    {
        return _map.Values.ToList();
    }

    public Dictionary<string, string> GetAllMappings()
    {
        return new Dictionary<string, string>(_map);
    }

    /// <summary>
    /// Returns true if the name is a type, interface, or namespace
    /// that should always be renamed regardless of context.
    /// </summary>
    public bool IsAlwaysRename(string name)
    {
        return _alwaysRenameNames.Contains(name);
    }

    public bool IsInterfaceMember(string name)
    {
        return _interfaceMemberNames.Contains(name);
    }

    private static int ComputeSeed(string uuid)
    {
        var input = Encoding.UTF8.GetBytes(uuid + "athena-obfs");
        var hash = SHA256.HashData(input);
        return BitConverter.ToInt32(hash, 0);
    }

    private static string GenerateUniqueName(
        Random rng,
        HashSet<string> used)
    {
        var length = 2;
        while (true)
        {
            var candidate = GenerateCandidate(rng, length);
            if (used.Add(candidate))
            {
                return candidate;
            }
            length++;
        }
    }

    private static string GenerateCandidate(Random rng, int length)
    {
        var sb = new StringBuilder(length + 1);
        sb.Append('_');
        for (var i = 0; i < length; i++)
        {
            sb.Append(AlphaNumChars[rng.Next(AlphaNumChars.Length)]);
        }
        return sb.ToString();
    }
}
