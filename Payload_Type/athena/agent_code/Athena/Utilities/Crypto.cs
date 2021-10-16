namespace Athena.Utilities
{
    /// <summary>
    /// Cryptography must be implemented on each profile to encrypt the data in transit to the
    /// server on top of whatever transport mechanism you use (such as TLS). A simple class
    /// will implement only the Encrypt and Decrypt functions.
    /// </summary>
    abstract public class Crypto
    {
        internal byte[] uuid;

        /// <summary>
        /// Return the UUID 
        /// </summary>
        internal string GetUUIDString()
        {
            return System.Text.ASCIIEncoding.ASCII.GetString(uuid);
        }

        /// <summary>
        /// Return the bytes of the UUID
        /// </summary>
        internal byte[] GetUUIDBytes()
        {
            return uuid;
        }

        /// <summary>
        /// Update the UUID for the Crypto object
        /// </summary>
        /// <param name="oldUID">Message to write</param>
        internal abstract void UpdateUUID(string oldUID);

        /// <summary>
        /// Encrypt a Mythic message
        /// </summary>
        /// <param name="plaintext">Message to encrypt</param>
        internal abstract string Encrypt(string plaintext);

        /// <summary>
        /// Decrypt a Mythic message
        /// </summary>
        /// <param name="encrypted">Message to decrypt</param>
        internal abstract string Decrypt(string encrypted);
    }
}
