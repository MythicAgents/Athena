using Agent.Models;
using IronPython.Hosting;
using IronPython.Modules;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using System.IO;
using System.Text;

namespace Agent.Managers
{
    public class PythonManager : IPythonManager
    {
        List<byte[]> loaded_libraries = new List<byte[]>();
        dynamic? _runtime;
        public PythonManager()
        {
            _runtime = IronPython.Hosting.Python.CreateRuntime();
        }

        public string ExecuteScript(string script, string[] args)
        {
            if (_runtime is null)
            {
                return "Not Initialized.";
            }

            try
            {
                using MemoryStream stdOut = new MemoryStream();
                var runtime = IronPython.Hosting.Python.CreateRuntime();

                runtime.IO.SetErrorOutput(stdOut, Encoding.ASCII);
                runtime.IO.SetOutput(stdOut, Encoding.ASCII);

                var engine = IronPython.Hosting.Python.GetEngine(runtime);
                var sysScope = engine.GetSysModule();
                var metaPath = sysScope.GetVariable("meta_path");

                foreach (var lib in loaded_libraries)
                {
                    try
                    {
                        metaPath.Add(new ByteArrayMetaPathImporter(lib));
                    }
                    catch
                    {
                    }
                }
                sysScope.SetVariable("meta_path", metaPath);
                sysScope.SetVariable("argv", args);

                ScriptSource ss = engine.CreateScriptSourceFromString(script, SourceCodeKind.AutoDetect);
                ss.Execute();

                return Encoding.ASCII.GetString(stdOut.ToArray());
            }
            catch (Exception e)
            {
                return "Error executing python: " + Environment.NewLine + e.ToString();
            }
        }

        public Task<string> ExecuteScriptAsync(string[] args, string script) =>
            throw new NotImplementedException();

        public bool LoadPyLib(byte[] bytes)
        {
            loaded_libraries.Add(bytes);
            return true;
        }
        public bool ClearPyLib()
        {
            loaded_libraries.Clear();
            return true;
        }
    }
}
