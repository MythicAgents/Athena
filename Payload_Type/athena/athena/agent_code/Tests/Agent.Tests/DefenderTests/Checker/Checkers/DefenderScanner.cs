using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Agent.Tests.Defender.Checker.Core;

namespace Agent.Tests.Defender.Checker.Checkers
{
    public class DefenderScanner : Scanner
    {
        byte[] FileBytes;
        string FilePath;
        private bool _malicious = false;
        public string badBytes = string.Empty;
        public DefenderScanResult result { get; set; }

        public DefenderScanner(byte[] file)
        {
            FileBytes = file;
        }

        public void Analyze()
        {
            if (!Directory.Exists(@"C:\Temp"))
            {
                Directory.CreateDirectory(@"C:\Temp");
            }

            FilePath = Path.Combine(@"C:\Temp", "file.exe");
            File.WriteAllBytes(FilePath, FileBytes);

            var status = ScanFile(FilePath);

            if (status.Result == ScanResult.NoThreatFound)
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
                File.WriteAllBytes(FilePath, splitArray);
                status = ScanFile(FilePath);

                if (status.Result == ScanResult.ThreatFound)
                {
                    var tmpArray = HalfSplitter(splitArray, lastgood, out this.badBytes);
                    Array.Resize(ref splitArray, tmpArray.Length);
                    Array.Copy(tmpArray, splitArray, tmpArray.Length);

                    this._malicious = true;
                }
                else if (status.Result == ScanResult.NoThreatFound)
                {
                    lastgood = splitArray.Length;
                    var tmpArray = Overshot(FileBytes, splitArray.Length);
                    Array.Resize(ref splitArray, tmpArray.Length);
                    Buffer.BlockCopy(tmpArray, 0, splitArray, 0, tmpArray.Length);
                }
            }
            this.result = status;  
        }

        public bool isMalicious()
        {
            return this._malicious;
        }

        public DefenderScanResult ScanFile(string file, bool getsig = false)
        {
            var result = new DefenderScanResult();

            if (!File.Exists(file))
            {
                result.Result = ScanResult.FileNotFound;
                return result;
            }

            var process = new Process();
            var mpcmdrun = new ProcessStartInfo(@"C:\Program Files\Windows Defender\MpCmdRun.exe")
            {
                Arguments = $"-Scan -ScanType 3 -File \"{file}\" -DisableRemediation -Trace -Level 0x10",
                CreateNoWindow = true,
                ErrorDialog = false,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            process.StartInfo = mpcmdrun;
            process.Start();
            process.WaitForExit(30000); //Wait 30s

            if (!process.HasExited)
            {
                process.Kill();
                result.Result = ScanResult.Timeout;
                return result;
            }

            if (getsig)
            {
                string stdout;
                string sigName;

                while ((stdout = process.StandardOutput.ReadLine()) != null)
                {
                    if (stdout.Contains("Threat  "))
                    {
                        string[] sig = stdout.Split(' ');
                        sigName = sig[19]; // Lazy way to get the signature name from MpCmdRun
                        result.Signature = sigName;
                        break;
                    }
                }
            }

            switch (process.ExitCode)
            {
                case 0:
                    result.Result = ScanResult.NoThreatFound;
                    break;
                case 2:
                    result.Result = ScanResult.ThreatFound;
                    break;
                default:
                    result.Result = ScanResult.Error;
                    break;
            }

            return result;
        }
    }

    public class DefenderScanResult
    {
        public ScanResult Result { get; set; }
        public string Signature { get; set; }
    }

    public enum ScanResult
    {
        NoThreatFound,
        ThreatFound,
        FileNotFound,
        Timeout,
        Error
    }
}