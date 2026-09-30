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
    public class AgentCrypto : ICryptoManager
    {
        private IAgentConfig config { get; set; }
        private ILogger logger { get; set; }

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
        public string Encrypt(string plaintext) =>
            Misc.Base64Encode(config.uuid + plaintext);

        /// <summary>
        /// Decrypt a string which has been encrypted with the PSK.
        /// </summary>
        /// <param name="encrypted">The encrypted string.</param>
        /// <returns></returns>
        public string Decrypt(string encrypted) =>
            Misc.Base64Decode(encrypted).Substring(config.uuid.Length);
    }
}
