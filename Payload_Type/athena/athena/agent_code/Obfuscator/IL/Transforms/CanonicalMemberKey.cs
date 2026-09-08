using System.Globalization;
using Mono.Cecil;

namespace Obfuscator.IL.Transforms;

internal static class CanonicalMemberKey
{
    public static string MethodSignature(
        string methodName,
        int genericArity,
        IEnumerable<TypeReference> parameterTypes)
        => $"{methodName}``{genericArity}({string.Join(",",
            parameterTypes.Select(TypeSignature))})";

    private static string TypeSignature(TypeReference type)
        => type switch
        {
            ByReferenceType byReference =>
                $"{TypeSignature(byReference.ElementType)}&",
            PointerType pointer =>
                $"{TypeSignature(pointer.ElementType)}*",
            ArrayType array => ArraySignature(array),
            GenericParameter parameter =>
                $"{(parameter.Type == GenericParameterType.Method ? "!!" : "!")}{
                    parameter.Position}",
            GenericInstanceType instance =>
                $"{TypeSignature(instance.ElementType)}<{
                    string.Join(",", instance.GenericArguments.Select(TypeSignature))}>",
            OptionalModifierType optional =>
                $"modopt({TypeSignature(optional.ModifierType)}){
                    TypeSignature(optional.ElementType)}",
            RequiredModifierType required =>
                $"modreq({TypeSignature(required.ModifierType)}){
                    TypeSignature(required.ElementType)}",
            PinnedType pinned => $"pinned({TypeSignature(pinned.ElementType)})",
            SentinelType sentinel =>
                $"sentinel({TypeSignature(sentinel.ElementType)})",
            FunctionPointerType functionPointer =>
                $"fnptr[{functionPointer.CallingConvention};{
                    (functionPointer.HasThis ? "instance" : "static")};{
                    (functionPointer.ExplicitThis ? "explicit" : "implicit")}]({
                    string.Join(",", functionPointer.Parameters.Select(parameter =>
                        TypeSignature(parameter.ParameterType)))})->{
                    TypeSignature(functionPointer.ReturnType)}",
            _ => NamedTypeSignature(type),
        };

    private static string ArraySignature(ArrayType array)
        => $"{TypeSignature(array.ElementType)}[array;{
            (array.IsVector ? "vector" : "nonvector")};dimensions={
            string.Join(",", array.Dimensions.Select(dimension =>
                $"({BoundSignature(dimension.LowerBound)},{
                    BoundSignature(dimension.UpperBound)})"))}]";

    private static string BoundSignature(int? bound)
        => bound.HasValue
            ? bound.Value.ToString(CultureInfo.InvariantCulture)
            : "null";

    private static string NamedTypeSignature(TypeReference type)
    {
        var name = type.DeclaringType is not null
            ? $"{NamedTypeSignature(type.DeclaringType)}/{type.Name}"
            : string.IsNullOrEmpty(type.Namespace)
                ? type.Name
                : $"{type.Namespace}.{type.Name}";

        if (type.DeclaringType is not null)
            return name;

        return $"[{AssemblySimpleName(type)}]{name}";
    }

    private static string AssemblySimpleName(TypeReference type)
        => type.Scope switch
        {
            AssemblyNameReference assembly => assembly.Name,
            ModuleDefinition module => module.Assembly.Name.Name,
            ModuleReference => type.Module.Assembly.Name.Name,
            _ => type.Module.Assembly.Name.Name,
        };
}
