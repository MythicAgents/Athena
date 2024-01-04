using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    public class Native
    {
        // Keychain related constants
        public const int errSecSuccess = 0;
        public const int kSecUnlockStateStatus = 0x00000001;
        public const int kSecReturnAttributes = 0x00000002;
        public const int kSecClassInternetPassword = 8;

        // Keychain function imports
        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        public static extern int SecKeychainUnlock(IntPtr keychain, uint passwordLength, byte[] password, int usePassword);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        public static extern int SecKeychainCopyDefault(ref IntPtr keychain);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        public static extern int SecKeychainItemCopyContent(IntPtr itemRef, ref int length, ref IntPtr data);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        public static extern int SecKeychainFindGenericPassword(IntPtr keychainOrArray, uint serviceNameLength, byte[] serviceName, uint accountNameLength, byte[] accountName, ref int passwordLength, ref IntPtr passwordData, IntPtr itemRef);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        public static extern int SecKeychainSearchCreateFromAttributes(IntPtr keychainOrArray, int keychainClass, IntPtr attrList, ref IntPtr searchRef);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        public static extern int SecKeychainSearchCopyNext(IntPtr searchRef, ref IntPtr itemRef);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        public static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        public static extern int SecKeychainItemFreeContent(IntPtr data);
    }
}
