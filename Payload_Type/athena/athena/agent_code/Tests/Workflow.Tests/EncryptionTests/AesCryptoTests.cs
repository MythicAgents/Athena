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
    }
}
