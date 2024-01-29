namespace Agent.Interfaces
{
    //This interface controls the encryption and decryption of mythic messages
    public interface ICryptoManager
    {
        public abstract string Encrypt(string data);
        public abstract string Decrypt(string data);
    }
}
