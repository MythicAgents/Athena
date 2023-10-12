using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Athena.Commands;
using Athena.Commands.Models;

namespace Plugins
{


    public class Python : IPlugin
    {

        //Going to need to change this to use MMAP to load the python311.dll
        [DllImport("C:\\Users\\checkymander\\Downloads\\python-3.11.4-embed-amd64\\python311.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Py_Initialize();

        [DllImport("C:\\Users\\checkymander\\Downloads\\python-3.11.4-embed-amd64\\python311.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Py_Finalize();

        [DllImport("C:\\Users\\checkymander\\Downloads\\python-3.11.4-embed-amd64\\python311.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyRun_String(
            [MarshalAs(UnmanagedType.LPStr)] string code,
            int start,
            IntPtr globals,
            IntPtr locals
        );

        [DllImport("C:\\Users\\checkymander\\Downloads\\python-3.11.4-embed-amd64\\python311.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyRun_SimpleString(
            [MarshalAs(UnmanagedType.LPStr)] string code
        );
        [DllImport("C:\\Users\\checkymander\\Downloads\\python-3.11.4-embed-amd64\\python311.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyImport_AddModule([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport("C:\\Users\\checkymander\\Downloads\\python-3.11.4-embed-amd64\\python311.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyImport_ImportModule(
            [MarshalAs(UnmanagedType.LPStr)] string name
        );

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyModule_GetDict(IntPtr module);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyDict_GetItemString(IntPtr dict, string key);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyObject_Str(IntPtr obj);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyObject_GetAttrString(IntPtr obj, string name);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyObject_CallObject(IntPtr obj, IntPtr args);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyObject_GetIter(IntPtr obj);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyIter_Next(IntPtr obj);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyIter_GetIter(IntPtr obj);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyIter_GetNext(IntPtr obj);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyIter_Close(IntPtr obj);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyIter_Check(IntPtr obj);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyIter_Type();

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyList_New(int size);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr PyList_GetItem(IntPtr list, int index);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_SetItem(IntPtr list, int index, IntPtr obj);

        [DllImport("python3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int PyList_Size(IntPtr list);

        public string Name => "python";
        public void Execute(Dictionary<string, string> args)
        {
            Py_Initialize();

            try
            {
                string pythonCode = "print(\"Hello from Python!\")";
                PyRun_SimpleString(pythonCode);
                //IntPtr result = PyRun_String(pythonCode, 0, IntPtr.Zero, IntPtr.Zero);

                Py_Finalize();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            TaskResponseHandler.Write("", args["task-id"], true);
            return;
        }

        private void LoadModule()
        {
            // Create a Python module object
            IntPtr module = PyImport_AddModule("__mymodule__");
            IntPtr globals = PyModule_GetDict(module);

            byte[] pythonModuleBytes = new byte[0];
            
            // Convert byte array to Python string
            string pythonCode = System.Text.Encoding.UTF8.GetString(pythonModuleBytes);

            // Execute Python code from the byte array
            PyRun_String(pythonCode, 0, globals, globals);
        }

        private void ExecutePythonCode()
        {
            string pythonCode = "print(\"Hello from Python!\")";
            PyRun_SimpleString(pythonCode);
            //IntPtr result = PyRun_String(pythonCode, 0, IntPtr.Zero, IntPtr.Zero);
        }

        private void ExecutePythonCodeWithArgs()
        {
            string pythonCode = "print(\"Hello from Python!\")";
            PyRun_SimpleString(pythonCode);
            //IntPtr result = PyRun_String(pythonCode, 0, IntPtr.Zero, IntPtr.Zero);
        }

        private void ExecutePythonCodeWithArgsAndReturn()
        {
            string pythonCode = "print(\"Hello from Python!\")";
            PyRun_SimpleString(pythonCode);
            //IntPtr result = PyRun_String(pythonCode, 0, IntPtr.Zero, IntPtr.Zero);
        }

        private void ExecutePythonCodeWithArgsAndReturnList()
        {
            string pythonCode = "print(\"Hello from Python!\")";
            PyRun_SimpleString(pythonCode);
            //IntPtr result = PyRun_String(pythonCode, 0, IntPtr.Zero, IntPtr.Zero);
        }

        private void ExecutePythonCodeWithArgsAndReturnDict()
        {
            string pythonCode = "print(\"Hello from Python!\")";
            PyRun_SimpleString(pythonCode);
            //IntPtr result = PyRun_String(pythonCode, 0, IntPtr.Zero, IntPtr.Zero);
        }

        private void ExecutePythonCodeWithArgsAndReturnObject()
        {
            string pythonCode = "print(\"Hello from Python!\")";
            PyRun_SimpleString(pythonCode);
            //IntPtr result = PyRun_String(pythonCode, 0, IntPtr.Zero, IntPtr.Zero);
        }

        private void ExecutePythonCodeWithArgsAndReturnObjectList()
        {
            string pythonCode = "print(\"Hello from Python!\")";
            PyRun_SimpleString(pythonCode);
            //IntPtr result = PyRun_String(pythonCode, 0, IntPtr.Zero, IntPtr.Zero);
        }

        private void ExecutePythonCodeWithArgsAndReturnObjectDict()
        {
            //string pythonCode
        }
    }
}
