using System;
using System.IO;
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
    public class AgentCrypto : ICryptoManager
    {
        private const int IvLength = 16;
        private const int HmacLength = 32;

        /// <summary>
        /// Pre-shared key given to us by God to identify
        /// ourselves to the mothership. When transferring
        /// C2 Profiles, thsi key must remain the same across
        /// Profile.Crypto classes.
        /// </summary>
        private byte[] PSK = { 0x00 };
        private IAgentConfig config { get; set; }
        private ILogger logger { get; set; }

        private byte[] uuid = Array.Empty<byte>();

        public AgentCrypto(IAgentConfig config, ILogger logger)
        {
            this.logger = logger;
            this.config = config;
            UpdateKeyMaterial();
            this.config.SetAgentConfigUpdated += OnAgentConfigUpdated;
        }

        private void OnAgentConfigUpdated(object? sender, EventArgs e) => UpdateKeyMaterial();

        private void UpdateKeyMaterial()
        {
            this.uuid = Encoding.ASCII.GetBytes(config.uuid);
            this.PSK = Convert.FromBase64String(config.psk);
        }

        /// <summary>
        /// Encrypt any given plaintext with the PSK given
        /// to the agent.
        /// </summary>
        /// <param name="plaintext">Plaintext to encrypt.</param>
        /// <returns>Enrypted string.</returns>
        public string Encrypt(string plaintext)
        {
            using Aes scAes = Aes.Create();
            // Use our PSK (generated in Apfell payload config) as the AES key
            scAes.Key = Convert.FromBase64String(config.psk);
            using ICryptoTransform encryptor = scAes.CreateEncryptor(scAes.Key, scAes.IV);
            using MemoryStream encryptMemStream = new MemoryStream();
            using CryptoStream encryptCryptoStream = new CryptoStream(encryptMemStream, encryptor, CryptoStreamMode.Write);
            using (StreamWriter encryptStreamWriter = new StreamWriter(encryptCryptoStream))
            {
                encryptStreamWriter.Write(plaintext);
            }

            // We need to send uuid:iv:ciphertext:hmac
            byte[] encrypted = scAes.IV.Concat(encryptMemStream.ToArray()).ToArray();
            using HMACSHA256 sha256 = new HMACSHA256(PSK);
            byte[] hmac = sha256.ComputeHash(encrypted);
            byte[] final = uuid.Concat(encrypted).Concat(hmac).ToArray();
            return Convert.ToBase64String(final);
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

            byte[] IV = new byte[IvLength];
            Array.Copy(input, uuidLength, IV, 0, IvLength);

            byte[] ciphertext = new byte[input.Length - uuidLength - IvLength - HmacLength];
            Array.Copy(input, uuidLength + IvLength, ciphertext, 0, ciphertext.Length);

            byte[] hmac = new byte[HmacLength];
            Array.Copy(input, uuidLength + IvLength + ciphertext.Length, hmac, 0, HmacLength);

            using HMACSHA256 sha256 = new HMACSHA256(PSK);
            byte[] computedHmac = sha256.ComputeHash(IV.Concat(ciphertext).ToArray());
            if (!hmac.SequenceEqual(computedHmac))
            {
                return string.Empty;
            }

            using Aes scAes = Aes.Create();
            scAes.Key = PSK;
            using ICryptoTransform decryptor = scAes.CreateDecryptor(scAes.Key, IV);
            using MemoryStream decryptMemStream = new MemoryStream(ciphertext);
            using CryptoStream decryptCryptoStream = new CryptoStream(decryptMemStream, decryptor, CryptoStreamMode.Read);
            using StreamReader decryptStreamReader = new StreamReader(decryptCryptoStream);
            return decryptStreamReader.ReadToEnd();
        }
    }
}
