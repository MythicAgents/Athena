using System;

namespace Athena
{
    public class Config
    {
        public string uuid { get; set; }
        public string userAgent { get; set; }
        public string hostHeader { get; set; }
        public int sleep { get; set; }
        public int jitter { get; set; }
        public string getURL { get; set; }
        public string postURL { get; set; }
        public string psk { get; set; }
        public string param { get; set; }


        public Config()
        {
            this.uuid = "41d82757-7a20-49b0-84a4-6f6947abd2c0";
            this.userAgent = "%USERAGENT%";
            this.hostHeader = "%HOSTHEADER%";
            this.sleep = Int32.Parse("10");
            this.jitter = Int32.Parse("23");
            this.getURL = "http://10.10.50.43/index";
            this.postURL = "http://10.10.50.43/data";
            this.psk = "%PSK%";
            this.param = "%QUERYPATHNAME%";
        }
        //Maybe add a named pipe config?

        //#region Mythic
        //// public static string uuid = "%UUID%";
        //public static string userAgent = "%USERAGENT%";
        //public static string hostHeader = "%HOSTHEADER%";
        //public static int sleep = Int32.Parse("10");
        //public static int jitter = Int32.Parse("23");
        ////public static int sleep = Int32.Parse("%SLEEP%");
        ////public static int jitter = Int32.Parse("%JITTER%");
        ////public static DateTime killDate = DateTime.Parse("%KILLDATE%");
        ////public static bool defaultProxy = bool.Parse("%DEFAULTPROXY%");
        ////public static string proxyAddress = "%PROXYADDRESS";
        ////public static string proxyUser = "%PROXYUSER%";
        ////public static string proxyPassword = "%PROXYPASS%";
        //public static string getURL = "http://10.10.50.43/index";
        ////public static string getUrl = "%GETURL%";
        //public static string postURL = "http://10.10.50.43/data";
        ////public static string postURL = "%POSTURL%";
        //public static string psk = "%PSK%";
        ////Necessary?
        //public static string param = "%QUERYPATHNAME%";
        ////public static int chunkSize = Int32.Parse("%CHUNKSIZE%");
        ////public static List<Server> servers = new List<Server> { };
        //#endregion

    }
}
