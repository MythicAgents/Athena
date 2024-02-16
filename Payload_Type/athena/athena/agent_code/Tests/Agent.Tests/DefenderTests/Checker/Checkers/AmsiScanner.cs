using System;
using System.Text;
using Agent.Tests.Defender.Checker.PInvoke;
using Agent.Tests.Defender.Checker.Core;


namespace Agent.Tests.Defender.Checker.Checkers
{
    class AmsiScanner : Scanner, IDisposable
    {
        IntPtr AmsiContext;
        IntPtr AmsiSession;
        private bool _malicious = false;
        public string badBytes = string.Empty;
        NativeMethods.AMSI_RESULT result { get; set; }
        byte[] FileBytes;

        public AmsiScanner(string appName = "ThreatCheck")
        {
            NativeMethods.AmsiInitialize(appName, out AmsiContext);
            NativeMethods.AmsiOpenSession(AmsiContext, out AmsiSession);
        }

        public void AnalyzeBytes(byte[] bytes)
        {
            FileBytes = bytes;

            var status = ScanBuffer(FileBytes);

            if (status != NativeMethods.AMSI_RESULT.AMSI_RESULT_DETECTED)
            {
                this._malicious = false;
                return;
            }
            else
            {
                this._malicious = true;
            }

            var splitArray = new byte[FileBytes.Length / 2];
            Buffer.BlockCopy(FileBytes, 0, splitArray, 0, FileBytes.Length / 2);
            var lastgood = 0;

            while (!Complete)
            {
                var detectionStatus = ScanBuffer(splitArray);

                if (detectionStatus == NativeMethods.AMSI_RESULT.AMSI_RESULT_DETECTED)
                {
                    var tmpArray = HalfSplitter(splitArray, lastgood, out this.badBytes);
                    Array.Resize(ref splitArray, tmpArray.Length);
                    Array.Copy(tmpArray, splitArray, tmpArray.Length);
                }
                else
                {
                    lastgood = splitArray.Length;
                    var tmpArray = Overshot(FileBytes, splitArray.Length); //Create temp array with 1.5x more bytes
                    Array.Resize(ref splitArray, tmpArray.Length);
                    Buffer.BlockCopy(tmpArray, 0, splitArray, 0, tmpArray.Length);
                }
            }
        }

        NativeMethods.AMSI_RESULT ScanBuffer(byte[] buffer)
        {
            NativeMethods.AmsiScanBuffer(AmsiContext, buffer, (uint)buffer.Length, "sample", AmsiSession, out NativeMethods.AMSI_RESULT result);
            return result;
        }

        NativeMethods.AMSI_RESULT ScanBuffer(byte[] buffer, IntPtr session)
        {
            NativeMethods.AmsiScanBuffer(AmsiContext, buffer, (uint)buffer.Length, "sample", session, out NativeMethods.AMSI_RESULT result);
            return result;
        }
        public bool isMalicious()
        {
            return this._malicious;
        }
        public bool RealTimeProtectionEnabled
        {
            get
            {
                var sample = Encoding.UTF8.GetBytes("Invoke-Expression 'AMSI Test Sample: 7e72c3ce-861b-4339-8740-0ac1484c1386'");
                var result = ScanBuffer(sample, IntPtr.Zero);

                if (result != NativeMethods.AMSI_RESULT.AMSI_RESULT_DETECTED)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        public void Dispose()
        {
            NativeMethods.AmsiCloseSession(AmsiContext, AmsiSession);
            NativeMethods.AmsiUninitialize(AmsiContext);
        }
    }
}