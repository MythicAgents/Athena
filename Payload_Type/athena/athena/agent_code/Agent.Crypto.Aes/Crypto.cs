using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Agent.Interfaces;




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
            PSK = Convert.FromBase64String(config.psk);
            uuid = ASCIIEncoding.ASCII.GetBytes(config.uuid);
            this.logger = logger;
            this.config = config;
            this.config.SetAgentConfigUpdated += OnAgentConfigUpdated;
        }

        private void OnAgentConfigUpdated(object? sender, EventArgs e)
        {
            this.uuid = ASCIIEncoding.ASCII.GetBytes(config.uuid);
            PSK = Convert.FromBase64String(config.psk);
        }

        /// <summary>
        /// Encrypt any given plaintext with the PSK given
        /// to the agent.
        /// </summary>
        /// <param name="plaintext">Plaintext to encrypt.</param>
        /// <returns>Enrypted string.</returns>
        public string Encrypt(string plaintext)
        {
            using (Aes scAes = Aes.Create())
            {
                // Use our PSK (generated in Apfell payload config) as the AES key
                //scAes.Key = PSK;
                scAes.Key = Convert.FromBase64String(config.psk);
                ICryptoTransform encryptor = scAes.CreateEncryptor(scAes.Key, scAes.IV);

                using (MemoryStream encryptMemStream = new MemoryStream())

                using (CryptoStream encryptCryptoStream = new CryptoStream(encryptMemStream, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter encryptStreamWriter = new StreamWriter(encryptCryptoStream))
                        encryptStreamWriter.Write(plaintext);
                    // We need to send uuid:iv:ciphertext:hmac
                    // Concat iv:ciphertext
                    byte[] encrypted = scAes.IV.Concat(encryptMemStream.ToArray()).ToArray();
                    HMACSHA256 sha256 = new HMACSHA256(PSK);
                    // Attach hmac to iv:ciphertext
                    byte[] hmac = sha256.ComputeHash(encrypted);
                    // Attach uuid to iv:ciphertext:hmac
                    byte[] final = uuid.Concat(encrypted.Concat(hmac).ToArray()).ToArray();
                    // Return base64 encoded ciphertext
                    return Convert.ToBase64String(final);
                }
            }
        }

        /// <summary>
        /// Decrypt a string which has been encrypted with the PSK.
        /// </summary>
        /// <param name="encrypted">The encrypted string.</param>
        /// <returns></returns>
        public string Decrypt(string encrypted)
        {
            byte[] input = Convert.FromBase64String(encrypted);

            int uuidLength = uuid.Length;
            byte[] uuidInput = new byte[uuidLength];
            Array.Copy(input, uuidInput, uuidLength);

            byte[] IV = new byte[16];
            Array.Copy(input, uuidLength, IV, 0, 16);

            byte[] ciphertext = new byte[input.Length - uuidLength - 16 - 32];
            Array.Copy(input, uuidLength + 16, ciphertext, 0, ciphertext.Length);

            HMACSHA256 sha256 = new HMACSHA256(PSK);
            byte[] hmac = new byte[32];
            Array.Copy(input, uuidLength + 16 + ciphertext.Length, hmac, 0, 32);

            if (Convert.ToBase64String(hmac) == Convert.ToBase64String(sha256.ComputeHash(IV.Concat(ciphertext).ToArray())))
            {
                using (Aes scAes = Aes.Create())
                {
                    scAes.Key = PSK;

                    ICryptoTransform decryptor = scAes.CreateDecryptor(scAes.Key, IV);

                    using (MemoryStream decryptMemStream = new MemoryStream(ciphertext))
                    using (CryptoStream decryptCryptoStream = new CryptoStream(decryptMemStream, decryptor, CryptoStreamMode.Read))
                    using (StreamReader decryptStreamReader = new StreamReader(decryptCryptoStream))
                    {
                        string decrypted = decryptStreamReader.ReadToEnd();
                        return decrypted;
                    }
                }
            }
            else
            {
                return String.Empty;
            }
        }
    }
}
