using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Models
{
    public enum InteractiveMessageType
    {
       Input = 0,
       Output = 1,
       Error = 2,
       Exit = 3,
       Escape = 4,    //^[ 0x1B
       CtrlA = 5,     //^A - 0x01 - start
       CtrlB = 6,     //^B - 0x02 - back
       CtrlC = 7,     //^C - 0x03 - interrupt process
       CtrlD = 8,     //^D - 0x04 - delete (exit if nothing sitting on input)
       CtrlE = 9,     //^E - 0x05 - end
       CtrlF = 10,     //^F - 0x06 - forward
       CtrlG = 11,     //^G - 0x07 - cancel search
       Backspace = 12, //^H - 0x08 - backspace
       Tab = 13,       //^I - 0x09 - tab
       CtrlK = 14,     //^K - 0x0B - kill line forwards
       CtrlL = 15,     //^L - 0x0C - clear screen
       CtrlN = 16,     //^N - 0x0E - next history
       CtrlP = 17,     //^P - 0x10 - previous history
       CtrlQ = 18,     //^Q - 0x11 - unpause output
       CtrlR = 19,     //^R - 0x12 - search history
       CtrlS = 20,     //^S - 0x13 - pause output
       CtrlU = 21,     //^U - 0x15 - kill line backwards
       CtrlW = 22,     //^W - 0x17 - kill word backwards
       CtrlY = 23,     //^Y - 0x19 - yank
       CtrlZ = 24,     //^Z - 0x1A - suspend process
    }
}
