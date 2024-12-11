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
                MemoryStream stdOut = new MemoryStream();
                var runtime = IronPython.Hosting.Python.CreateRuntime();

                runtime.IO.SetErrorOutput(stdOut, Encoding.ASCII);
                runtime.IO.SetOutput(stdOut, Encoding.ASCII);

                var engine = IronPython.Hosting.Python.GetEngine(runtime);
                var sysScope = engine.GetSysModule();
                var metaPath = sysScope.GetVariable("meta_path");
                //MemoryStream stdOut = new MemoryStream();
                //ScriptScope engine = IronPython.Hosting.Python.GetEngine(_runtime);
                //_runtime.IO.SetErrorOutput(stdOut, Encoding.ASCII);
                //_runtime.IO.SetOutput(stdOut, Encoding.ASCII);
                //var runtime = engine.IO.SetOutput(stdOut, Encoding.Default);
                //IronPython.Hosting.Python.GetSysModule(engine);
                //var sysScope = engine.Get
                //var sysScope = engine.GetSysModule();
                //var scope = engine.CreateScope();
                //var metaPath = engine.GetVariable("meta_path");

                foreach (var lib in loaded_libraries)
                {
                    try
                    {
                        var importer = new ByteArrayMetaPathImporter(lib);
                        metaPath.Add(importer);
                    }
                    catch
                    {
                    }
                }
                sysScope.SetVariable("meta_path", metaPath);
                sysScope.SetVariable("argv", args);
                //engine.SetVariable("meta_path", metaPath);
                //engine.SetVariable("argv", args);
                //engine.argv = args;
                ScriptSource ss = engine.CreateScriptSourceFromString(script, SourceCodeKind.AutoDetect);
                //ScriptSource ss = engine.CreateScriptSourceFromString(script, SourceCodeKind.AutoDetect);
                ss.Execute();

                return Encoding.ASCII.GetString(stdOut.ToArray());
            }
            catch (Exception e)
            {
                return "Error executing python: " + Environment.NewLine + e.ToString();
            }
        }

        public Task<string> ExecuteScriptAsync(string[] args, string script)
        {
            throw new NotImplementedException();
        }

        public bool LoadPyLib(byte[] bytes)
        {
            loaded_libraries.Add(bytes);
            //if(_metaPath is null || _sysScope is null)
            //{
            //    return false;
            //}

            //try
            //{
            //    var importer = new ByteArrayMetaPathImporter(bytes);
            //    _metaPath.Add(importer);
            //    _sysScope.SetVariable("meta_path", _metaPath);
            //}
            //catch
            //{
            //    return false;
            //}
            return true;
        }
        public bool ClearPyLib()
        {
            loaded_libraries.Clear();
            return true;
        }
    }
}
