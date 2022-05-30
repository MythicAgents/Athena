using System.Runtime.InteropServices;

namespace Athena.Utilities
{
    public class Pinvoke
    {
        #region Windows

        #endregion

        #region Mac
        #endregion

        #region Nix
        [DllImport("libc")]
        public static extern uint geteuid();
        #endregion
    }
}
