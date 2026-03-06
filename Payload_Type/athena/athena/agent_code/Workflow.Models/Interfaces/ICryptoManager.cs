namespace Workflow.Contracts
{
    //This interface controls the encryption and decryption of mythic messages
    public interface ISecurityProvider
    {
        public abstract string Encrypt(string data);
        public abstract string Decrypt(string data);
    }
}
