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
        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSString_UTF8String(IntPtr str);

        [DllImport("/System/Library/Frameworks/OSAKit.framework/OSAKit")]
        public static extern IntPtr OSALanguage_languageForName(string name);

        [DllImport("/System/Library/Frameworks/OSAKit.framework/OSAKit")]
        public static extern IntPtr OSAScript_alloc();

        [DllImport("/System/Library/Frameworks/OSAKit.framework/OSAKit")]
        public static extern IntPtr OSAScript_initWithSourceLanguage(IntPtr script, IntPtr source, IntPtr language);

        [DllImport("/System/Library/Frameworks/OSAKit.framework/OSAKit")]
        public static extern IntPtr OSAScript_executeAndReturnError(IntPtr script, out IntPtr error);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSDictionary_count(IntPtr dictionary);

        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        public static extern IntPtr NSDictionary_objectForKey(IntPtr dictionary, IntPtr key);
    }
}
