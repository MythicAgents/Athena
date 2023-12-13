using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Athena.Models.Comms.Tasks
{
    public class InteractiveMessage
    {
        public string task_id { get; set; }
        public string data { get; set; }
        public MessageType message_type { get; set; }
    }
    public enum MessageType
    {
        Input,
        Output,
        Error,
        Exit,
        Escape,
        CtrlA,
        CtrlB,
        CtrlC,
        CtrlD,
        CtrlE,
        CtrlF,
        CtrlG,
        Backspace,
        Tab,
        CtrlK,
        CtrlL,
        CtrlN,
        CtrlP,
        CtrlQ,
        CtrlR,
        CtrlS,
        CtrlU,
        CtrlW,
        CtrlY,
        CtrlZ,
        end
    }
}
