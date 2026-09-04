using Agent.Config;
using Agent.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agent.Tests;

[TestClass]
public class AssemblyIdentityTests
{
    [TestMethod]
    public void NameIsDeterministicFromBuildAgentUuidAndLogicalName()
    {
        Assert.AreEqual(
            "_a0lHf",
            AssemblyIdentity.GetObfuscatedName(
                "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
                "echo"));
    }

    [TestMethod]
    public void BuildAgentUuidDoesNotChangeAfterCheckinUuidUpdate()
    {
        var config = new AgentConfig();
        var buildAgentUuid = config.build_uuid;
        config.uuid = Guid.NewGuid().ToString();
        Assert.AreEqual(buildAgentUuid, config.build_uuid);
    }

    [TestMethod]
    public void LoadCandidatesPreferUuidDerivedNameAndRetainPlainFallback()
    {
        CollectionAssert.AreEqual(
            new[] { "_a0lHf", "echo" },
            AssemblyIdentity.GetLoadCandidates(
                "37eb846a-12b9-45d5-a49c-8e10754cc0ba",
                "echo").ToArray());
    }
}
