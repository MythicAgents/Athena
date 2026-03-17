using Workflow.Models;

namespace Workflow.Tests.PluginTests
{
    [TestClass]
    public class SshReconTests : PluginTestBase
    {
        public SshReconTests()
        {
            LoadPlugin("ssh-recon");
        }

        [TestMethod]
        public async Task SshRecon_EnumKeys_WithTempDir()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ssh_test_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllText(
                    Path.Combine(tempDir, "id_rsa"),
                    "-----BEGIN RSA PRIVATE KEY-----\nfakekey\n-----END RSA PRIVATE KEY-----");
                File.WriteAllText(
                    Path.Combine(tempDir, "id_rsa.pub"),
                    "ssh-rsa AAAA... user@host");

                var job = CreateJob("ssh-recon", new
                {
                    action = "ssh-keys",
                    path = tempDir
                });
                var response = await ExecuteAndGetResponse(job);
                AssertSuccess(response);
                AssertOutputContains(response, "id_rsa");
                AssertOutputContains(response, "is_private_key");
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public async Task SshRecon_UnknownAction_ReturnsError()
        {
            var job = CreateJob("ssh-recon", new { action = "invalid" });
            var response = await ExecuteAndGetResponse(job);
            AssertError(response);
        }
    }
}
