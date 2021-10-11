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


        internal string GetUUIDString()
        {
            return System.Text.ASCIIEncoding.ASCII.GetString(uuid);
        }

        internal byte[] GetUUIDBytes()
        {
            return uuid;
        }

        internal abstract void UpdateUUID(string oldUID);
        internal abstract string Encrypt(string plaintext);
        internal abstract string Decrypt(string encrypted);
    }
}
