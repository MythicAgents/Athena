using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Obfuscator.IL;

internal static class CliSignatureSafety
{
    internal static void Validate(byte[] assemblyBytes, string fileName)
    {
        using var stream = new MemoryStream(assemblyBytes, writable: false);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var provider = UnsafeArrayProvider.Instance;

        foreach (var handle in reader.MethodDefinitions)
        {
            var signature = reader.GetMethodDefinition(handle).DecodeSignature(provider, (object?)null);
            RejectIf(signature.ReturnType || signature.ParameterTypes.Any(value => value),
                fileName, handle);
        }

        foreach (var handle in reader.FieldDefinitions)
            RejectIf(reader.GetFieldDefinition(handle).DecodeSignature(provider, (object?)null),
                fileName, handle);

        foreach (var handle in reader.PropertyDefinitions)
        {
            var signature = reader.GetPropertyDefinition(handle).DecodeSignature(provider, (object?)null);
            RejectIf(signature.ReturnType || signature.ParameterTypes.Any(value => value),
                fileName, handle);
        }

        foreach (var handle in reader.MemberReferences)
        {
            var member = reader.GetMemberReference(handle);
            var unsafeSignature = member.GetKind() == MemberReferenceKind.Method
                ? HasUnsafe(member.DecodeMethodSignature(provider, (object?)null))
                : member.DecodeFieldSignature(provider, (object?)null);
            RejectIf(unsafeSignature, fileName, handle);
        }

        for (var row = 1; row <= reader.GetTableRowCount(TableIndex.TypeSpec); row++)
        {
            var handle = MetadataTokens.TypeSpecificationHandle(row);
            RejectIf(reader.GetTypeSpecification(handle).DecodeSignature(provider, (object?)null),
                fileName, handle);
        }

        for (var row = 1; row <= reader.GetTableRowCount(TableIndex.StandAloneSig); row++)
        {
            var handle = MetadataTokens.StandaloneSignatureHandle(row);
            var signature = reader.GetStandaloneSignature(handle);
            var unsafeSignature = signature.GetKind() == StandaloneSignatureKind.LocalVariables
                ? signature.DecodeLocalSignature(provider, (object?)null).Any(value => value)
                : HasUnsafe(signature.DecodeMethodSignature(provider, (object?)null));
            RejectIf(unsafeSignature, fileName, handle);
        }

        for (var row = 1; row <= reader.GetTableRowCount(TableIndex.MethodSpec); row++)
        {
            var handle = MetadataTokens.MethodSpecificationHandle(row);
            RejectIf(reader.GetMethodSpecification(handle)
                    .DecodeSignature(provider, (object?)null).Any(value => value),
                fileName, handle);
        }
    }

    private static bool HasUnsafe(MethodSignature<bool> signature) =>
        signature.ReturnType || signature.ParameterTypes.Any(value => value);

    private static void RejectIf(bool unsafeSignature, string fileName, Handle handle)
    {
        if (!unsafeSignature)
            return;

        var table = handle.Kind switch
        {
            HandleKind.MethodDefinition => "MethodDef",
            HandleKind.FieldDefinition => "Field",
            HandleKind.PropertyDefinition => "Property",
            HandleKind.MemberReference => "MemberRef",
            HandleKind.TypeSpecification => "TypeSpec",
            HandleKind.StandaloneSignature => "StandAloneSig",
            HandleKind.MethodSpecification => "MethodSpec",
            _ => handle.Kind.ToString(),
        };
        throw new NotSupportedException(
            $"Assembly '{Path.GetFileName(fileName)}' contains a Cecil-unsafe unsized rank-one CLI ARRAY signature in {table} token 0x{MetadataTokens.GetToken(handle):X8}.");
    }

    private sealed class UnsafeArrayProvider : ISignatureTypeProvider<bool, object?>
    {
        internal static readonly UnsafeArrayProvider Instance = new();

        public bool GetArrayType(bool elementType, ArrayShape shape) =>
            elementType || (shape.Rank == 1 && shape.Sizes.IsEmpty && shape.LowerBounds.IsEmpty);
        public bool GetByReferenceType(bool elementType) => elementType;
        public bool GetFunctionPointerType(MethodSignature<bool> signature) => HasUnsafe(signature);
        public bool GetGenericInstantiation(bool genericType, ImmutableArray<bool> typeArguments) =>
            genericType || typeArguments.Any(value => value);
        public bool GetGenericMethodParameter(object? genericContext, int index) => false;
        public bool GetGenericTypeParameter(object? genericContext, int index) => false;
        public bool GetModifiedType(bool modifier, bool unmodifiedType, bool isRequired) =>
            modifier || unmodifiedType;
        public bool GetPinnedType(bool elementType) => elementType;
        public bool GetPointerType(bool elementType) => elementType;
        public bool GetPrimitiveType(PrimitiveTypeCode typeCode) => false;
        public bool GetSZArrayType(bool elementType) => elementType;
        public bool GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle,
            byte rawTypeKind) => false;
        public bool GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle,
            byte rawTypeKind) => false;
        public bool GetTypeFromSpecification(MetadataReader reader, object? genericContext,
            TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}
