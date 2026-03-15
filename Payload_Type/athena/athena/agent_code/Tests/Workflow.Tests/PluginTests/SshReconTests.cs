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
        public async Task SshRecon_AuthorizedKeysRead_WithTempFile()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "authorized_keys_test");
            try
            {
                File.WriteAllText(tempFile, "ssh-rsa AAAA testkey");
                var job = CreateJob("ssh-recon", new
                {
                    action = "authorized-keys-read",
                    path = tempFile
                });
                var response = await ExecuteAndGetResponse(job);
                AssertSuccess(response);
                AssertOutputContains(response, "ssh-rsa AAAA testkey");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [TestMethod]
        public async Task SshRecon_ReadMissingFile_ReportsNotFound()
        {
            var job = CreateJob("ssh-recon", new
            {
                action = "authorized-keys-read",
                path = "/nonexistent/path/authorized_keys"
            });
            var response = await ExecuteAndGetResponse(job);
            AssertSuccess(response);
            AssertOutputContains(response, "not found");
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
