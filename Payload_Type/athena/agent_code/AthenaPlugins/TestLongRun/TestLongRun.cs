using PluginBase;
using System;
using System.Collections.Generic;
using System.Text;

namespace Plugin
{
    //maybe this for clipboard monitoring? https://github.com/Willy-Kimura/SharpClipboard
    //Keylogger: https://github.com/fabriciorissetto/KeystrokeAPI/blob/master/Keystroke.API/KeystrokeAPI.cs
    public static class TestLongRunning
    {
        private static StringBuilder sb = new StringBuilder();
        private static bool isRunning = false;

        public static void Execute(Dictionary<string, object> args) //Probably an issue when you try running two long running processes at once
        {

            if (isRunning)
            {
                //return new ResponseResult
                //{
                //    completed = "true",
                //    user_output = "An instance of this plugin running",
                //    task_id = (string)args["task-id"], //task-id passed in from Athena
                //    status = "error"
                //};
            }
            isRunning = true;
            try
            {





                //Return a successful response
                //return new ResponseResult
                //{
                //    completed = "true",
                //    user_output = sb.ToString(),
                //    task_id = (string)args["task-id"], //task-id passed in from Athena
                //};
            }
            catch (Exception e)
            {
                isRunning = false;
                //oh no an error
                //return new ResponseResult
                //{
                //    completed = "true",
                //    user_output = e.Message,
                //    task_id = (string)args["task-id"],
                //    status = "error"
                //};
            }
        }

        public static bool IsRunning()
        {
            return true;
        }

        public static ResponseResult GetOutput(string task_id)
        {
            string output = sb.ToString();
            sb.Clear();

            return new ResponseResult
            {
                completed = "true",
                user_output = output,
                task_id = task_id, //task-id passed in from Athena
            };
        }
    }

}
