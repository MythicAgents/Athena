using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Obfuscator.IL;
using Obfuscator.IL.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class CliSignatureSafetyTests
{
    [TestMethod]
    public void MetadataTransform_UnsafeMethodDefIsRejectedBeforeCecilWrite()
    {
        var bytes = BuildFixture("Unsafe", FixtureKind.MethodDef);

        Assert.ThrowsExactly<NotSupportedException>(() =>
            new MetadataManglingTransform(42).Transform(bytes));
    }

    [TestMethod]
    public void CrossReferenceTransform_UnsafeMethodDefIsRejectedBeforeCecilWrite()
    {
        var bytes = BuildFixture("Unsafe", FixtureKind.MethodDef);

        Assert.ThrowsExactly<NotSupportedException>(() =>
            new CrossReferenceTransform().PatchReferences(
                bytes,
                new Dictionary<string, Dictionary<string, string>>(),
                null));
    }

    [TestMethod]
    public void RewriteBatch_UnsafeMethodDefFailsBeforeAnyDirectoryMutation()
    {
        var dir = CreateTempDir();
        try
        {
            var unsafePath = Path.Combine(dir, "Unsafe.dll");
            File.WriteAllBytes(unsafePath, BuildFixture("Unsafe", FixtureKind.MethodDef));
            File.WriteAllBytes(Path.Combine(dir, "Safe.dll"), BuildFixture("Safe", FixtureKind.Safe));
            var mapPath = Path.Combine(dir, "map.json");
            File.WriteAllText(mapPath, "{\"metadataRenames\":{\"keep\":\"value\"}}");
            var before = Snapshot(dir);

            var error = Assert.ThrowsExactly<NotSupportedException>(() =>
                new ILRewriter().RewriteBatch(dir, 42, mapPath, ["Unsafe", "Safe"]));

            StringAssert.Contains(error.Message, "Unsafe.dll");
            StringAssert.Contains(error.Message, "MethodDef");
            StringAssert.Contains(error.Message, "0x06000002");
            AssertSnapshot(before, dir);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void Rewrite_UnsafeMethodDefFailsBeforeDllOrMapMutation()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "Unsafe.dll");
            File.WriteAllBytes(path, BuildFixture("Unsafe", FixtureKind.MethodDef));
            var map = Path.Combine(dir, "map.json");
            File.WriteAllText(map, "keep");
            var before = Snapshot(dir);

            Assert.ThrowsExactly<NotSupportedException>(() =>
                new ILRewriter().Rewrite(path, 42, map));

            AssertSnapshot(before, dir);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void RenameAll_UnsafeMethodDefFailsBeforeCecilEmitOrRename()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "Unsafe.dll"),
                BuildFixture("Unsafe", FixtureKind.MethodDef));
            var before = Snapshot(dir);

            Assert.ThrowsExactly<NotSupportedException>(() =>
                new AssemblyRenameTransform(42).RenameAll(dir, ["Unsafe"], []));

            AssertSnapshot(before, dir);
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    [DataRow(FixtureKind.Field, "Field", "0x04000001")]
    [DataRow(FixtureKind.Property, "Property", "0x17000001")]
    [DataRow(FixtureKind.MemberRefMethod, "MemberRef", "0x0A000001")]
    [DataRow(FixtureKind.MemberRefField, "MemberRef", "0x0A000001")]
    [DataRow(FixtureKind.StandaloneLocal, "StandAloneSig", "0x11000001")]
    [DataRow(FixtureKind.StandaloneCalli, "StandAloneSig", "0x11000001")]
    [DataRow(FixtureKind.NestedByRefGeneric, "TypeSpec", "0x1B000001")]
    [DataRow(FixtureKind.NestedFunctionPointer, "TypeSpec", "0x1B000001")]
    [DataRow(FixtureKind.MethodSpec, "MethodSpec", "0x2B000001")]
    public void Scanner_RejectsUnsafeArraysAcrossSignatureShapes(
        FixtureKind kind, string table, string token)
    {
        var error = Assert.ThrowsExactly<NotSupportedException>(() =>
            CliSignatureSafety.Validate(BuildFixture("Fixture", kind), "Fixture.dll"));

        StringAssert.Contains(error.Message, table);
        StringAssert.Contains(error.Message, token);
    }

    [TestMethod]
    public void Scanner_AllowsSzArray()
    {
        CliSignatureSafety.Validate(BuildFixture("Safe", FixtureKind.Safe), "Safe.dll");
    }

    public enum FixtureKind
    {
        Safe, MethodDef, Field, Property, MemberRefMethod, MemberRefField,
        StandaloneLocal, StandaloneCalli, NestedByRefGeneric,
        NestedFunctionPointer, MethodSpec,
    }

    private static byte[] BuildFixture(string identity, FixtureKind kind)
    {
        var md = new MetadataBuilder();
        var il = new BlobBuilder();
        var bodies = new MethodBodyStreamEncoder(il);
        var code = new BlobBuilder();
        new InstructionEncoder(code).OpCode(ILOpCode.Ret);
        var body = bodies.AddMethodBody(new InstructionEncoder(code));
        md.AddModule(0, md.GetOrAddString(identity + ".dll"),
            md.GetOrAddGuid(Guid.NewGuid()), default, default);
        md.AddAssembly(md.GetOrAddString(identity), new Version(1, 0, 0, 0),
            default, default, 0, AssemblyHashAlgorithm.None);
        var core = typeof(object).Assembly.GetName();
        var coreRef = md.AddAssemblyReference(md.GetOrAddString(core.Name!), core.Version!,
            default, default, 0, default);
        var objectType = md.AddTypeReference(coreRef, md.GetOrAddString("System"),
            md.GetOrAddString("Object"));
        md.AddTypeDefinition(TypeAttributes.NotPublic, default, md.GetOrAddString("<Module>"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        md.AddTypeDefinition(TypeAttributes.Public, md.GetOrAddString("Fixture"),
            md.GetOrAddString("Value"), objectType,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));

        if (kind == FixtureKind.Field)
            md.AddFieldDefinition(FieldAttributes.Public, md.GetOrAddString("Value"),
                Sig(md, 0x06, 0x14, 0x08, 0x01, 0x00, 0x00));
        if (kind == FixtureKind.Property)
        {
            md.AddProperty(PropertyAttributes.None, md.GetOrAddString("Value"),
                Sig(md, 0x08, 0x00, 0x14, 0x08, 0x01, 0x00, 0x00));
            md.AddPropertyMap(MetadataTokens.TypeDefinitionHandle(2),
                MetadataTokens.PropertyDefinitionHandle(1));
        }
        if (kind == FixtureKind.MemberRefMethod)
            md.AddMemberReference(objectType, md.GetOrAddString("Unsafe"),
                Sig(md, 0x00, 0x00, 0x14, 0x08, 0x01, 0x00, 0x00));
        if (kind == FixtureKind.MemberRefField)
            md.AddMemberReference(objectType, md.GetOrAddString("Unsafe"),
                Sig(md, 0x06, 0x14, 0x08, 0x01, 0x00, 0x00));

        md.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL, md.GetOrAddString("Safe"),
            Sig(md, 0x00, 0x01, 0x01, 0x1d, 0x08), body, MetadataTokens.ParameterHandle(1));
        if (kind == FixtureKind.MethodDef)
            md.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static,
                MethodImplAttributes.IL, md.GetOrAddString("Unsafe"),
                Sig(md, 0x00, 0x01, 0x01, 0x14, 0x08, 0x01, 0x00, 0x00),
                body, MetadataTokens.ParameterHandle(1));
        if (kind == FixtureKind.StandaloneLocal)
            md.AddStandaloneSignature(Sig(md, 0x07, 0x01, 0x14, 0x08, 0x01, 0x00, 0x00));
        if (kind == FixtureKind.StandaloneCalli)
            md.AddStandaloneSignature(Sig(md, 0x00, 0x00, 0x14, 0x08, 0x01, 0x00, 0x00));
        if (kind == FixtureKind.NestedByRefGeneric)
            md.AddTypeSpecification(Sig(md,
                0x10, 0x15, 0x12, 0x05, 0x01, 0x14, 0x08, 0x01, 0x00, 0x00));
        if (kind == FixtureKind.NestedFunctionPointer)
            md.AddTypeSpecification(Sig(md,
                0x1b, 0x00, 0x00, 0x14, 0x08, 0x01, 0x00, 0x00));
        if (kind == FixtureKind.MethodSpec)
        {
            var genericMethod = md.AddMethodDefinition(
                MethodAttributes.Public | MethodAttributes.Static,
                MethodImplAttributes.IL, md.GetOrAddString("Generic"),
                Sig(md, 0x10, 0x01, 0x00, 0x01), body,
                MetadataTokens.ParameterHandle(1));
            md.AddGenericParameter(genericMethod, GenericParameterAttributes.None,
                md.GetOrAddString("T"), 0);
            md.AddMethodSpecification(genericMethod,
                Sig(md, 0x0a, 0x01, 0x14, 0x08, 0x01, 0x00, 0x00));
        }

        var pe = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage | Characteristics.Dll),
            new MetadataRootBuilder(md), il, flags: CorFlags.ILOnly);
        var output = new BlobBuilder();
        pe.Serialize(output);
        return output.ToArray();
    }

    private static BlobHandle Sig(MetadataBuilder md, params byte[] bytes)
    {
        var blob = new BlobBuilder();
        blob.WriteBytes(bytes);
        return md.GetOrAddBlob(blob);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "clisig_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static Dictionary<string, byte[]> Snapshot(string dir) =>
        Directory.GetFiles(dir).ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);

    private static void AssertSnapshot(Dictionary<string, byte[]> expected, string dir)
    {
        var actual = Snapshot(dir);
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), actual.Keys.ToArray());
        foreach (var (name, bytes) in expected)
            CollectionAssert.AreEqual(bytes, actual[name], name);
    }
}