using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent
{
    class Config
    {
        //public static int timer = 0;
        public static int port = 8080;
        public static string output;
        public static StreamWriter sw;
        public static string key = "definitelynotfarmer";
        public static string task_id = "";
        public static bool encrypt = false;
        public static string banner = @"";
    }
}
