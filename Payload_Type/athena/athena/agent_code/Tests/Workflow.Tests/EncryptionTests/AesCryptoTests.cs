using Workflow.Security;
using Workflow.Tests.TestClasses;
using Workflow.Tests.TestInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workflow.Tests.EncryptionTests
{
    [TestClass]
    public class AesCryptoTests
    {
        IEnumerable<IChannel> _profiles = new List<IChannel>() { new TestProfile() };
        IRequestDispatcher _taskManager = new TestRequestDispatcher();
        ILogger _logger = new TestLogger();
        IServiceConfig _config = new TestServiceConfig();
        ICredentialProvider _tokenManager = new TestCredentialProvider();
        ISecurityProvider _cryptoManager = new TestCryptoManager();
        IDataBroker _messageManager = new TestDataBroker();
        public AesCryptoTests()
        {

        }
        [TestMethod]
        public void TestEncryptDecrypt()
        {
            ISecurityProvider aesCryptManager = new SecurityProvider(_config, _logger);
            string testString = "I could not bring myself to fight my Father’s brother, Poseidon, quaking with anger at you, still enraged";

            string encryptedString = aesCryptManager.Encrypt(testString);
            string decryptedString = aesCryptManager.Decrypt(encryptedString);

            Assert.IsTrue(testString.Equals(decryptedString));
        }
        [TestMethod]
        public void TestEncryptDecrypt_EmptyString()
        {
            ISecurityProvider aesCryptManager = new SecurityProvider(_config, _logger);
            string testString = "";

            string encryptedString = aesCryptManager.Encrypt(testString);
            string decryptedString = aesCryptManager.Decrypt(encryptedString);

            Assert.IsTrue(testString.Equals(decryptedString));
        }

        [TestMethod]
        public void TestEncryptDecrypt_LargePayload()
        {
            ISecurityProvider aesCryptManager = new SecurityProvider(_config, _logger);
            string testString = Utilities.GenerateRandomText(100000);

            string encryptedString = aesCryptManager.Encrypt(testString);
            string decryptedString = aesCryptManager.Decrypt(encryptedString);

            Assert.AreEqual(testString, decryptedString);
        }

        [TestMethod]
        public void TestEncryptProducesDifferentOutput()
        {
            ISecurityProvider aesCryptManager = new SecurityProvider(_config, _logger);
            string testString = "test plaintext data";

            string encrypted1 = aesCryptManager.Encrypt(testString);
            string encrypted2 = aesCryptManager.Encrypt(testString);

            Assert.AreNotEqual(testString, encrypted1);
            Assert.AreNotEqual(testString, encrypted2);
        }

        [TestMethod]
        public void TestDifferentKeyProducesDifferentCiphertext()
        {
            ISecurityProvider crypto1 = new SecurityProvider(_config, _logger);

            var config2 = new TestServiceConfig();
            config2.psk = Convert.ToBase64String(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            ISecurityProvider crypto2 = new SecurityProvider(config2, _logger);

            string testString = "same plaintext different keys";

            string encrypted1 = crypto1.Encrypt(testString);
            string encrypted2 = crypto2.Encrypt(testString);

            string decrypted1 = crypto1.Decrypt(encrypted1);

            Assert.AreEqual(testString, decrypted1);
            Assert.AreNotEqual(encrypted1, encrypted2);
        }
    }
}
