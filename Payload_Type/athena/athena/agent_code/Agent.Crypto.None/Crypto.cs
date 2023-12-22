using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using Agent.Interfaces;

using Agent.Utilities;



//Credit: Dwight Hohnstein from Apollo
//https://github.com/djhohnstein
//https://twitter.com/djhohnstein
//https://github.com/MythicAgents/Apollo/

namespace Agent.Crypto
{
    /// <summary>
    /// Encryption handler for the Default profile type.
    /// </summary>
    /// <summary>
    /// Encryption handler for the Default profile type.
    /// </summary>
    public class AgentCrypto : ICryptoManager
    {
        /// <summary>
        /// Pre-shared key given to us by God to identify
        /// ourselves to the mothership. When transferring
        /// C2 Profiles, thsi key must remain the same across
        /// Profile.Crypto classes.
        /// </summary>
        private byte[] PSK = { 0x00 };
        private IAgentConfig config { get; set; }
        private ILogger logger { get; set; }

        private byte[] uuid;

        public AgentCrypto(IAgentConfig config, ILogger logger)
        {
            this.logger = logger;
            this.config = config;
        }

        /// <summary>
        /// Encrypt any given plaintext with the PSK given
        /// to the agent.
        /// </summary>
        /// <param name="plaintext">Plaintext to encrypt.</param>
        /// <returns>Enrypted string.</returns>
        public string Encrypt(string plaintext)
        {
            return Misc.Base64Encode(config.uuid + plaintext);
        }

        /// <summary>
        /// Decrypt a string which has been encrypted with the PSK.
        /// </summary>
        /// <param name="encrypted">The encrypted string.</param>
        /// <returns></returns>
        public string Decrypt(string encrypted)
        {
            return Misc.Base64Decode(encrypted).Substring(config.uuid.Length);
        }
    }
}
