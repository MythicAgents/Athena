using System.Runtime.InteropServices;

namespace Agent.Managers
{
    public static class Native
    {
        [DllImport("libc")]
        public static extern uint geteuid();
    }
}
