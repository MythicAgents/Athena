using Obfuscator.Config;

namespace Obfuscator.Tests;

[TestClass]
public sealed class ContractScannerTests
{
    [TestMethod]
    public void Scan_TraversesNestedMemberShapesDeterministically()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IPlugin
            {
                Agent.Models.Root Execute();
            }
            """,
            """
            namespace Agent.Models;
            public class Root
            {
                public (Child?, IReadOnlyList<Leaf[]>) Payload { get; set; }
            }
            public class Child
            {
                public Leaf? Value { get; set; }
            }
            public class Leaf { }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(
            new[] { "Child", "Leaf", "Root" }, names.Types);
        CollectionAssert.AreEqual(
            new[] { "Execute", "Payload", "Value" },
            names.InterfaceMembers);
    }

    [TestMethod]
    public void Scan_DoesNotCrossIntoTypesOutsideSharedContractNamespaces()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IPlugin
            {
                Agent.Models.Root Execute();
            }
            """,
            """
            namespace Agent.Models;
            public class Root
            {
                public External.Library.ExternalDto External { get; set; }
                public Uri Endpoint { get; set; }
            }
            """,
            """
            namespace External.Library;
            public class ExternalDto
            {
                public string ExternalMember { get; set; }
            }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(new[] { "Root" }, names.Types);
        CollectionAssert.DoesNotContain(
            names.InterfaceMembers, "ExternalMember");
    }

    [TestMethod]
    public void Scan_IncludesBidirectionalPluginInterfaceFamilyAndReachableDtos()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IBasePlugin
            {
                Agent.Models.Dto Execute();
            }
            public interface IPlugin : IBasePlugin { }
            public interface ILeftPlugin : IPlugin { }
            public interface IRightPlugin : IBasePlugin { }
            public interface IDiamondPlugin : ILeftPlugin, IRightPlugin { }
            public interface IUnrelated { Agent.Models.Decoy Ignore(); }
            """,
            """
            namespace Agent.Models;
            public class Dto { public Nested Value { get; set; } }
            public class Nested { }
            public class Decoy { }
            """,
            """
            namespace Agent.Interfaces.Child;
            public interface IPlugin { Agent.Models.Decoy Spoof(); }
            public interface IChildOnly : IPlugin { }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(
            new[]
            {
                "IBasePlugin", "IDiamondPlugin", "ILeftPlugin", "IPlugin",
                "IRightPlugin",
            },
            names.Interfaces);
        CollectionAssert.AreEqual(new[] { "Execute", "Value" }, names.InterfaceMembers);
        CollectionAssert.AreEqual(new[] { "Dto", "Nested" }, names.Types);
        CollectionAssert.AreEqual(
            new[]
            {
                "Agent.Interfaces.IBasePlugin",
                "Agent.Interfaces.IDiamondPlugin",
                "Agent.Interfaces.ILeftPlugin",
                "Agent.Interfaces.IPlugin",
                "Agent.Interfaces.IRightPlugin",
                "Agent.Models.Dto",
                "Agent.Models.Nested",
            },
            names.ContractDeclarations.Select(item => item.MetadataName).ToArray());
    }

    [TestMethod]
    public void Scan_DoesNotAdmitSiblingDescendantOfPluginAncestor()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IBase
            {
                Agent.Models.Root Shared();
            }
            public interface IPlugin : IBase { }
            public interface ISibling : IBase
            {
                Agent.Models.Decoy SiblingOnly();
            }
            """,
            """
            namespace Agent.Models;
            public class Root { }
            public class Decoy { }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(
            new[] { "IBase", "IPlugin" }, names.Interfaces);
        CollectionAssert.AreEqual(new[] { "Root" }, names.Types);
        CollectionAssert.DoesNotContain(names.InterfaceMembers, "SiblingOnly");
    }

    [TestMethod]
    public void Scan_AcceptsOnlyExactCanonicalPluginRoot()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IPlugin { Agent.Models.Real Execute(); }
            """,
            """
            namespace Agent.Models;
            public interface IPlugin { FalseDto Spoof(); }
            public interface IFalsePlugin : IPlugin { }
            public class Real { }
            public class FalseDto { }
            """,
            """
            namespace Agent.Interfaces.Child;
            public interface IPlugin { Agent.Models.ChildDto Spoof(); }
            public interface IChildPlugin : IPlugin { }
            """,
            """
            namespace Agent.Models;
            public class ChildDto { }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(new[] { "IPlugin" }, names.Interfaces);
        CollectionAssert.AreEqual(new[] { "Real" }, names.Types);
        CollectionAssert.AreEqual(new[] { "Execute" }, names.InterfaceMembers);
    }

    [TestMethod]
    public void Scan_IncludesInheritedWireMembersAndTheirTypes()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IPlugin
            {
                Agent.Models.Derived Execute();
            }
            """,
            """
            namespace Agent.Models;
            public class BaseDto
            {
                public Nested Inherited;
            }
            public class Derived : BaseDto { }
            public class Nested { }
            public class Unrelated { public Decoy Ignore; }
            public class Decoy { }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(
            new[] { "BaseDto", "Derived", "Nested" }, names.Types);
        CollectionAssert.Contains(names.InterfaceMembers, "Inherited");
        CollectionAssert.DoesNotContain(names.Types, "Unrelated");
        CollectionAssert.DoesNotContain(names.Types, "Decoy");
    }

    [TestMethod]
    public void Scan_TraversesDelegateTypeParameterConstraints()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IPlugin
            {
                Agent.Models.Callback<Agent.Models.Concrete> Handler { get; }
            }
            """,
            """
            namespace Agent.Models;
            public delegate void Callback<T>(T value) where T : IConstraintDto;
            public interface IConstraintDto
            {
                Nested Required { get; }
            }
            public class Concrete : IConstraintDto
            {
                public Nested Required { get; set; }
            }
            public class Nested { }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(
            new[] { "Callback", "Concrete", "IConstraintDto", "Nested" },
            names.Types);
        CollectionAssert.Contains(names.InterfaceMembers, "Required");
    }

    [TestMethod]
    public void Scan_TraversesInheritedIndexerParameterTypesAndConstraints()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IIndexed
            {
                Agent.Models.ValueDto this[Agent.Models.Key<Agent.Models.KeyLeaf> key] { get; }
            }
            public interface IPlugin : IIndexed { }
            """,
            """
            namespace Agent.Models;
            public class KeyConstraint { public ConstraintLeaf Required { get; set; } }
            public class Key<T> where T : KeyConstraint { }
            public class KeyLeaf : KeyConstraint { }
            public class ValueDto { }
            public class ConstraintLeaf { }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(
            new[] { "ConstraintLeaf", "Key", "KeyConstraint", "KeyLeaf", "ValueDto" },
            names.Types);
    }

    [TestMethod]
    public void Scan_TraversesConstructedPluginAncestorTypeArguments()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IBase<T>
            {
                T Execute();
            }
            public interface IPlugin : IBase<Agent.Models.Payload> { }
            """,
            """
            namespace Agent.Models;
            public class Payload
            {
                public Nested Value { get; set; }
            }
            public class Nested { }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(new[] { "Nested", "Payload" }, names.Types);
        CollectionAssert.Contains(names.InterfaceMembers, "Value");
    }

    [TestMethod]
    public void Scan_TraversesGenericMethodConstraints()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IPlugin
            {
                T Execute<T>() where T : Agent.Models.ConstraintDto;
            }
            """,
            """
            namespace Agent.Models;
            public class ConstraintDto
            {
                public Nested Required { get; set; }
            }
            public class Nested { }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(
            new[] { "ConstraintDto", "Nested" }, names.Types);
        CollectionAssert.Contains(names.InterfaceMembers, "Required");
    }

    [TestMethod]
    public void Scan_TraversesDelegateSignaturesWithoutSimpleNameSpoofing()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IPlugin
            {
                Agent.Models.Callback<Agent.Models.Leaf> Handler { get; }
            }
            """,
            """
            namespace Agent.Models;
            public delegate Result Callback<T>(
                Envelope payload,
                System.Collections.Generic.IReadOnlyList<T[]> items);
            public class Envelope { public Detail Value { get; set; } }
            public class Detail { }
            public class Leaf { }
            public class Result { public Envelope Echo { get; set; } }
            """,
            """
            namespace Agent.Models.Spoof;
            public delegate Decoy Callback<T>(T value);
            public class Decoy { }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(
            new[] { "Callback", "Detail", "Envelope", "Leaf", "Result" },
            names.Types);
        CollectionAssert.AreEqual(
            new[] { "Echo", "Handler", "Value" }, names.InterfaceMembers);
        Assert.IsTrue(names.ContractDeclarations.Any(item =>
            item.MetadataName == "Agent.Models.Callback`1"));
        Assert.IsFalse(names.ContractDeclarations.Any(item =>
            item.MetadataName == "Agent.Models.Spoof.Callback`1"));
        CollectionAssert.DoesNotContain(names.Types, "Decoy");
    }

    [TestMethod]
    public void Scan_TerminatesOnCyclicDtoGraphsWithoutDuplicates()
    {
        using var contracts = ContractSources.Create(
            """
            namespace Agent.Interfaces;
            public interface IPlugin
            {
                Agent.Models.Root Execute();
            }
            """,
            """
            namespace Agent.Models;
            public class Root
            {
                public Node Next { get; set; }
            }
            public class Node
            {
                public Root Owner { get; set; }
            }
            """);

        var names = ContractScanner.Scan(contracts.Path);

        CollectionAssert.AreEqual(
            new[] { "Node", "Root" }, names.Types);
        CollectionAssert.AreEqual(
            new[] { "Execute", "Next", "Owner" },
            names.InterfaceMembers);
    }

    private sealed class ContractSources : IDisposable
    {
        private ContractSources(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static ContractSources Create(params string[] sources)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"contract_scanner_{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            for (var index = 0; index < sources.Length; index++)
                File.WriteAllText(
                    System.IO.Path.Combine(path, $"Contract{index}.cs"),
                    sources[index]);
            return new ContractSources(path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
