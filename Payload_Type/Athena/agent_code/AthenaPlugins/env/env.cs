using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Athena
{
    public static class Plugin
    {

        public static string Execute(Dictionary<string, object> args)
        {                                  
            string Output = "";
            foreach (DictionaryEntry fs in Environment.GetEnvironmentVariables())
            {
                Output += (fs.Key.ToString() + " = " + fs.Value.ToString() + Environment.NewLine);
            }
            return Output;

        }
    }
}
