using System.Runtime.InteropServices;

namespace Workflow
{
    internal static class CredentialsNative
    {
        [DllImport("vaultcli.dll")]
        internal static extern int VaultEnumerateVaults(
            int flags, out int vaultCount, out IntPtr vaultGuids);

        [DllImport("vaultcli.dll")]
        internal static extern int VaultOpenVault(
            ref Guid vaultGuid, int flags, out IntPtr vaultHandle);

        [DllImport("vaultcli.dll")]
        internal static extern int VaultEnumerateItems(
            IntPtr vaultHandle, int flags, out int itemCount, out IntPtr items);

        [DllImport("vaultcli.dll")]
        internal static extern int VaultCloseVault(ref IntPtr vaultHandle);

        [DllImport("vaultcli.dll")]
        internal static extern int VaultFree(IntPtr memory);

        [StructLayout(LayoutKind.Sequential)]
        internal struct VAULT_ITEM
        {
            public Guid SchemaId;
            public IntPtr pszCredentialFriendlyName;
            public IntPtr pResourceElement;
            public IntPtr pIdentityElement;
            public IntPtr pAuthenticatorElement;
            public IntPtr pPackageSid;
            public long LastModified;
            public int dwFlags;
            public int dwPropertiesCount;
            public IntPtr pPropertyElements;
        }
    }
}
