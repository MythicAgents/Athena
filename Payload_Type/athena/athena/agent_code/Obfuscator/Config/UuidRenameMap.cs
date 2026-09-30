using System.Security.Cryptography;
using System.Text;

namespace Obfuscator.Config;

public sealed class UuidRenameMap
{

    internal static bool TryNormalizeUuid(
        string? uuid, out string normalizedUuid)
    {
        if (Guid.TryParse(uuid?.Trim(), out var parsed))
        {
            normalizedUuid = parsed.ToString("D");
            return true;
        }

        normalizedUuid = string.Empty;
        return false;
    }

    private readonly Dictionary<string, string> _map;
    private readonly HashSet<string> _alwaysRenameNames;
    private readonly HashSet<string> _interfaceMemberNames;
    private readonly HashSet<string> _contractTypeNames;
    private readonly HashSet<string> _contractSymbolNames;
    private readonly HashSet<string> _contractNamespaces;
    private readonly HashSet<string> _contractMetadataNames;
    private readonly HashSet<ContractDeclarationKey> _contractDeclarations;

    private UuidRenameMap(
        Dictionary<string, string> map,
        HashSet<string> alwaysRename,
        HashSet<string> interfaceMembers,
        HashSet<string> contractTypes,
        HashSet<string> contractSymbolNames,
        HashSet<string> contractNamespaces,
        HashSet<string> contractMetadataNames,
        HashSet<ContractDeclarationKey> contractDeclarations)
    {
        _map = map;
        _alwaysRenameNames = alwaysRename;
        _interfaceMemberNames = interfaceMembers;
        _contractTypeNames = contractTypes;
        _contractSymbolNames = contractSymbolNames;
        _contractNamespaces = contractNamespaces;
        _contractMetadataNames = contractMetadataNames;
        _contractDeclarations = contractDeclarations;
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

        var symbols = new Dictionary<string, string>(StringComparer.Ordinal);
        AddSymbols(symbols, "interface", names.Interfaces);
        AddSymbols(symbols, "member", names.InterfaceMembers);
        AddSymbols(symbols, "type", names.Types);
        AddSymbols(symbols, "record-param", names.RecordParams);
        AddSymbols(symbols, "namespace", names.Namespaces);

        var normalizedUuid = NormalizeUuid(uuid);
        var used = new HashSet<string>();
        var map = new Dictionary<string, string>();

        foreach (var (name, identity) in symbols
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var renamed = GenerateUniqueName(
                normalizedUuid, identity, used);
            map[name] = renamed;
        }

        return new UuidRenameMap(
            map, alwaysRename, interfaceMembers,
            new HashSet<string>(names.Types),
            new HashSet<string>(names.Interfaces.Concat(names.Types)),
            new HashSet<string>(names.Namespaces),
            new HashSet<string>(names.ContractDeclarations
                .Select(declaration => declaration.MetadataName),
                StringComparer.Ordinal),
            names.ContractDeclarations.Select(declaration =>
                new ContractDeclarationKey(
                    declaration.MetadataName,
                    ContractScanner.NormalizeSourcePath(declaration.FilePath),
                    declaration.DeclarationRawKind,
                    declaration.DeclarationOrdinal)).ToHashSet());
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

    public bool IsContractType(string name)
    {
        return _contractTypeNames.Contains(name);
    }

    public bool IsContractSymbol(string name)
    {
        return _contractSymbolNames.Contains(name);
    }

    public bool IsContractNamespace(string name)
    {
        return _contractNamespaces.Contains(name);
    }

    public bool IsContractNamespaceOrChild(string name)
    {
        return _contractNamespaces.Any(contractNamespace =>
            name.Equals(contractNamespace, StringComparison.Ordinal)
            || name.StartsWith(
                contractNamespace + ".", StringComparison.Ordinal));
    }

    public bool HasContractDeclarationProvenance =>
        _contractMetadataNames.Count > 0;

    public bool IsCanonicalContractType(string metadataName) =>
        _contractMetadataNames.Contains(metadataName);

    public bool IsCanonicalContractDeclaration(
        string metadataName,
        string sourcePath,
        int declarationRawKind,
        int declarationOrdinal) =>
        _contractDeclarations.Contains(new ContractDeclarationKey(
            metadataName,
            ContractScanner.NormalizeSourcePath(sourcePath),
            declarationRawKind,
            declarationOrdinal));

    private static void AddSymbols(
        Dictionary<string, string> symbols,
        string category,
        IEnumerable<string> names)
    {
        foreach (var name in names.OrderBy(x => x, StringComparer.Ordinal))
            symbols.TryAdd(name, category + ":" + name);
    }

    internal static string NormalizeUuid(string uuid)
    {
        if (TryNormalizeUuid(uuid, out var normalizedUuid))
            return normalizedUuid;

        throw new ArgumentException(
            "UUID must be a valid GUID.", nameof(uuid));
    }

    private static string GenerateUniqueName(
        string uuid, string identity, HashSet<string> used)
    {
        for (var attempt = 0; ; attempt++)
        {
            var input = Encoding.UTF8.GetBytes(
                $"athena-contract-v1\0{uuid}\0{identity}\0{attempt}");
            var candidate = "_" + Convert.ToHexString(
                SHA256.HashData(input)).ToLowerInvariant()[..12];
            if (used.Add(candidate))
                return candidate;
        }
    }

    private sealed record ContractDeclarationKey(
        string MetadataName,
        string SourcePath,
        int DeclarationRawKind,
        int DeclarationOrdinal);
}
