using Agent.Crypto;
using Agent.Tests.TestClasses;
using Agent.Tests.TestInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests.EncryptionTests
{
    [TestClass]
    public class AesCryptoTests
    {
        IEnumerable<IProfile> _profiles = new List<IProfile>() { new TestProfile() };
        ITaskManager _taskManager = new TestTaskManager();
        ILogger _logger = new TestLogger();
        IAgentConfig _config = new TestAgentConfig();
        ITokenManager _tokenManager = new TestTokenManager();
        ICryptoManager _cryptoManager = new TestCryptoManager();
        IMessageManager _messageManager = new TestMessageManager();
        public AesCryptoTests()
        {

        }
        [TestMethod]
        public void TestEncryptDecrypt()
        {
            ICryptoManager aesCryptManager = new AgentCrypto(_config, _logger);
            string testString = "I could not bring myself to fight my Father’s brother, Poseidon, quaking with anger at you, still enraged";

            string encryptedString = aesCryptManager.Encrypt(testString);
            string decryptedString = aesCryptManager.Decrypt(encryptedString);

            Assert.IsTrue(testString.Equals(decryptedString));
        }
        [TestMethod]
        public void TestEncryptDecrypt_EmptyString()
        {
            ICryptoManager aesCryptManager = new AgentCrypto(_config, _logger);
            string testString = "";

            string encryptedString = aesCryptManager.Encrypt(testString);
            string decryptedString = aesCryptManager.Decrypt(encryptedString);

            Assert.IsTrue(testString.Equals(decryptedString));
        }
    }
}
