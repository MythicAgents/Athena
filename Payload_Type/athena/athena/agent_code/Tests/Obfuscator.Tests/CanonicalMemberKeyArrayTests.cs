using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using Obfuscator.IL.Transforms;

namespace Obfuscator.Tests;

[TestClass]
public class CanonicalMemberKeyArrayTests
{
    [TestMethod]
    public void MethodSignature_DistinguishesVectorFromRankOneNonVectorArray()
    {
        var vector = CreateArray();
        var nonVector = CreateArray(new ArrayDimension(0, null));

        var vectorSignature = Signature(vector);
        var nonVectorSignature = Signature(nonVector);

        Assert.AreEqual(
            "Select``0([ElementAssembly]Example.Element[array;vector;dimensions=(null,null)])",
            vectorSignature);
        Assert.AreEqual(
            "Select``0([ElementAssembly]Example.Element[array;nonvector;dimensions=(0,null)])",
            nonVectorSignature);
        Assert.AreNotEqual(vectorSignature, nonVectorSignature);
    }

    [TestMethod]
    public void MethodSignature_DistinguishesSameRankArraysWithDifferentLowerBounds()
    {
        var lowerNegative = Signature(CreateArray(new ArrayDimension(-1, 8)));
        var lowerZero = Signature(CreateArray(new ArrayDimension(0, 8)));

        Assert.AreEqual(
            "Select``0([ElementAssembly]Example.Element[array;nonvector;dimensions=(-1,8)])",
            lowerNegative);
        Assert.AreEqual(
            "Select``0([ElementAssembly]Example.Element[array;nonvector;dimensions=(0,8)])",
            lowerZero);
        Assert.AreNotEqual(lowerNegative, lowerZero);
    }

    [TestMethod]
    public void MethodSignature_DistinguishesSameLowerBoundWithDifferentUpperBounds()
    {
        var upperEight = Signature(CreateArray(new ArrayDimension(0, 8)));
        var upperNine = Signature(CreateArray(new ArrayDimension(0, 9)));

        Assert.AreEqual(
            "Select``0([ElementAssembly]Example.Element[array;nonvector;dimensions=(0,8)])",
            upperEight);
        Assert.AreEqual(
            "Select``0([ElementAssembly]Example.Element[array;nonvector;dimensions=(0,9)])",
            upperNine);
        Assert.AreNotEqual(upperEight, upperNine);
    }

    [TestMethod]
    public void MethodSignature_DistinguishesUnspecifiedBoundFromExplicitBound()
    {
        var unspecifiedLower = Signature(CreateArray(new ArrayDimension(null, 8)));
        var explicitLower = Signature(CreateArray(new ArrayDimension(0, 8)));

        Assert.AreEqual(
            "Select``0([ElementAssembly]Example.Element[array;nonvector;dimensions=(null,8)])",
            unspecifiedLower);
        Assert.AreEqual(
            "Select``0([ElementAssembly]Example.Element[array;nonvector;dimensions=(0,8)])",
            explicitLower);
        Assert.AreNotEqual(unspecifiedLower, explicitLower);
    }

    private static ArrayType CreateArray(params ArrayDimension[] dimensions)
    {
        var module = ModuleDefinition.CreateModule(
            "SignatureHost", ModuleKind.Dll);
        var elementType = new TypeReference(
            "Example",
            "Element",
            module,
            new AssemblyNameReference("ElementAssembly", new Version(1, 0)));
        var array = new ArrayType(elementType);

        if (dimensions.Length > 0)
        {
            array.Dimensions.Clear();
            foreach (var dimension in dimensions)
                array.Dimensions.Add(dimension);
        }

        return array;
    }

    private static string Signature(ArrayType array)
        => CanonicalMemberKey.MethodSignature("Select", 0, [array]);
}
