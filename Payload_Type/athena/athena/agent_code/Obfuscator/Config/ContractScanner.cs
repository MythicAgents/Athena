using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Obfuscator.Config;

/// <summary>
/// Scans Workflow.Models / Workflow.Contracts source files
/// with Roslyn to extract interface names, members, types,
/// namespaces, and record constructor parameters that the
/// obfuscator needs to rename consistently.
///
/// Only types that appear in interface method/property/event
/// signatures are included as "contract types".
///
/// Only members from "plugin-facing" interfaces are included:
/// - Interfaces in the IModule hierarchy
/// - Interfaces used as PluginContext parameter types
/// - IComponentProvider
/// This prevents generic names from infrastructure interfaces
/// (IServiceConfig.uuid, ITaskResponse.status) from causing
/// false renames in plugin code.
/// </summary>
public static class ContractScanner
{
    private static readonly HashSet<string> ContractNamespaces =
        ["Workflow.Contracts", "Workflow.Models"];

    public static ContractNames Scan(string contractsDir)
    {
        var interfaces = new HashSet<string>();
        var allInterfaceMembers =
            new Dictionary<string, List<string>>();
        var allInterfaceSignatureTypes =
            new Dictionary<string, HashSet<string>>();
        var namespaces = new HashSet<string>();
        var interfaceBases =
            new Dictionary<string, List<string>>();

        var allTypes =
            new Dictionary<string, TypeDeclarationInfo>();
        var allRecords =
            new Dictionary<string, RecordDeclarationSyntax>();

        var csFiles = Directory.EnumerateFiles(
            contractsDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(
                Path.DirectorySeparatorChar + "obj"
                    + Path.DirectorySeparatorChar));

        foreach (var file in csFiles)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetRoot();

            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case NamespaceDeclarationSyntax ns
                        when ContractNamespaces
                            .Contains(ns.Name.ToString()):
                        namespaces.Add(ns.Name.ToString());
                        break;

                    case FileScopedNamespaceDeclarationSyntax ns
                        when ContractNamespaces
                            .Contains(ns.Name.ToString()):
                        namespaces.Add(ns.Name.ToString());
                        break;

                    case InterfaceDeclarationSyntax iface:
                    {
                        var name = iface.Identifier.Text;
                        interfaces.Add(name);

                        var ifMembers = new List<string>();
                        var typeRefs = new HashSet<string>();
                        CollectInterfaceMembers(
                            iface, ifMembers, typeRefs);
                        allInterfaceMembers[name] = ifMembers;
                        allInterfaceSignatureTypes[name] =
                            typeRefs;

                        var bases = new List<string>();
                        if (iface.BaseList is not null)
                        {
                            foreach (var bt in
                                iface.BaseList.Types)
                            {
                                var baseName = bt.Type switch
                                {
                                    IdentifierNameSyntax id =>
                                        id.Identifier.Text,
                                    _ => null
                                };
                                if (baseName is not null)
                                    bases.Add(baseName);
                            }
                        }
                        interfaceBases[name] = bases;
                        break;
                    }

                    case ClassDeclarationSyntax cls:
                        allTypes[cls.Identifier.Text] =
                            new TypeDeclarationInfo(
                                cls.Identifier.Text);
                        break;

                    case RecordDeclarationSyntax rec:
                        allTypes[rec.Identifier.Text] =
                            new TypeDeclarationInfo(
                                rec.Identifier.Text);
                        allRecords[rec.Identifier.Text] = rec;
                        break;

                    case EnumDeclarationSyntax enm:
                        allTypes[enm.Identifier.Text] =
                            new TypeDeclarationInfo(
                                enm.Identifier.Text);
                        break;

                    case StructDeclarationSyntax str:
                        allTypes[str.Identifier.Text] =
                            new TypeDeclarationInfo(
                                str.Identifier.Text);
                        break;
                }
            }
        }

        // Find plugin-facing interfaces:
        // 1. IModule hierarchy (IModule + anything extending it)
        // 2. Interfaces used as PluginContext parameter types
        // 3. IComponentProvider
        var pluginFacing = FindPluginFacingInterfaces(
            interfaces, interfaceBases, allRecords);

        // Collect members only from plugin-facing interfaces
        var members = new HashSet<string>();
        var signatureTypeRefs = new HashSet<string>();
        foreach (var iface in pluginFacing)
        {
            if (allInterfaceMembers.TryGetValue(
                iface, out var ifaceMembers))
            {
                foreach (var m in ifaceMembers)
                    members.Add(m);
            }
            if (allInterfaceSignatureTypes.TryGetValue(
                iface, out var ifaceTypeRefs))
            {
                foreach (var t in ifaceTypeRefs)
                    signatureTypeRefs.Add(t);
            }
        }

        // Also collect signature types from ALL interfaces
        // (for determining contract types — even non-plugin-facing
        // interfaces reference contract types in their signatures)
        foreach (var (_, typeRefs) in allInterfaceSignatureTypes)
        {
            foreach (var t in typeRefs)
                signatureTypeRefs.Add(t);
        }

        // Only include types referenced in interface signatures
        var contractTypes = new List<string>();
        foreach (var typeRef in signatureTypeRefs)
        {
            if (allTypes.ContainsKey(typeRef)
                && !interfaces.Contains(typeRef))
            {
                contractTypes.Add(typeRef);
            }
        }

        // Include interface-like types from signatures that
        // aren't defined locally (e.g. ITaskResponse)
        foreach (var typeRef in signatureTypeRefs)
        {
            if (interfaces.Contains(typeRef))
                continue;
            if (!allTypes.ContainsKey(typeRef)
                && typeRef.StartsWith('I')
                && typeRef.Length > 1
                && char.IsUpper(typeRef[1]))
            {
                contractTypes.Add(typeRef);
            }
        }

        // Include records with parameter lists as contract types
        foreach (var (name, rec) in allRecords)
        {
            if (rec.ParameterList is { Parameters.Count: > 0 }
                && !interfaces.Contains(name)
                && !contractTypes.Contains(name))
            {
                contractTypes.Add(name);
            }
        }

        // Collect record params for contract-type records
        var recordParams = new List<string>();
        var allContractTypeNames = new HashSet<string>(
            interfaces.Concat(contractTypes));
        foreach (var (name, rec) in allRecords)
        {
            if (allContractTypeNames.Contains(name))
                CollectRecordParams(rec, recordParams);
        }

        return new ContractNames(
            interfaces.ToList(),
            members.ToList(),
            contractTypes,
            namespaces.ToList(),
            recordParams);
    }

    // Interfaces whose members have generic names that
    // collide with non-contract code (e.g. chunk_size, uuid,
    // sleep, debug, Log). Excluded from member collection
    // even though they appear as PluginContext parameters.
    private static readonly HashSet<string>
        MemberExcludedInterfaces =
            ["IServiceConfig", "ILogger"];

    private static HashSet<string> FindPluginFacingInterfaces(
        HashSet<string> allInterfaces,
        Dictionary<string, List<string>> interfaceBases,
        Dictionary<string, RecordDeclarationSyntax> allRecords)
    {
        var result = new HashSet<string>();

        // 1. Find IModule hierarchy
        if (allInterfaces.Contains("IModule"))
        {
            result.Add("IModule");
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var (name, bases) in interfaceBases)
                {
                    if (result.Contains(name))
                        continue;
                    if (bases.Any(b => result.Contains(b)))
                    {
                        result.Add(name);
                        changed = true;
                    }
                }
            }
        }

        // 2. Interfaces used as PluginContext parameter types
        //    (excluding ones with generic member names)
        if (allRecords.TryGetValue(
            "PluginContext", out var pluginCtx)
            && pluginCtx.ParameterList is not null)
        {
            foreach (var param in
                pluginCtx.ParameterList.Parameters)
            {
                if (param.Type is IdentifierNameSyntax id
                    && allInterfaces.Contains(
                        id.Identifier.Text)
                    && !MemberExcludedInterfaces.Contains(
                        id.Identifier.Text))
                {
                    result.Add(id.Identifier.Text);
                }
            }
        }

        // 3. IComponentProvider
        if (allInterfaces.Contains("IComponentProvider"))
            result.Add("IComponentProvider");

        return result;
    }

    private static void CollectInterfaceMembers(
        InterfaceDeclarationSyntax iface,
        List<string> members,
        HashSet<string> typeRefs)
    {
        foreach (var member in iface.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                    members.Add(method.Identifier.Text);
                    CollectTypeRefs(method.ReturnType, typeRefs);
                    foreach (var param in
                        method.ParameterList.Parameters)
                    {
                        if (param.Type is not null)
                            CollectTypeRefs(
                                param.Type, typeRefs);
                    }
                    break;
                case PropertyDeclarationSyntax prop:
                    members.Add(prop.Identifier.Text);
                    CollectTypeRefs(prop.Type, typeRefs);
                    break;
                case EventFieldDeclarationSyntax evt:
                    foreach (var v in evt.Declaration.Variables)
                        members.Add(v.Identifier.Text);
                    CollectTypeRefs(
                        evt.Declaration.Type, typeRefs);
                    break;
                case EventDeclarationSyntax evtDecl:
                    members.Add(evtDecl.Identifier.Text);
                    CollectTypeRefs(evtDecl.Type, typeRefs);
                    break;
            }
        }
    }

    private static void CollectTypeRefs(
        TypeSyntax type, HashSet<string> typeRefs)
    {
        switch (type)
        {
            case IdentifierNameSyntax id:
                typeRefs.Add(id.Identifier.Text);
                break;
            case GenericNameSyntax generic:
                foreach (var arg in
                    generic.TypeArgumentList.Arguments)
                    CollectTypeRefs(arg, typeRefs);
                break;
            case QualifiedNameSyntax qualified:
                CollectTypeRefs(qualified.Right, typeRefs);
                break;
            case ArrayTypeSyntax array:
                CollectTypeRefs(array.ElementType, typeRefs);
                break;
            case NullableTypeSyntax nullable:
                CollectTypeRefs(
                    nullable.ElementType, typeRefs);
                break;
        }
    }

    private static void CollectRecordParams(
        RecordDeclarationSyntax rec,
        List<string> recordParams)
    {
        if (rec.ParameterList is null)
            return;

        foreach (var param in rec.ParameterList.Parameters)
            recordParams.Add(param.Identifier.Text);
    }

    private record TypeDeclarationInfo(string Name);
}

public record ContractNames(
    List<string> Interfaces,
    List<string> InterfaceMembers,
    List<string> Types,
    List<string> Namespaces,
    List<string> RecordParams);
