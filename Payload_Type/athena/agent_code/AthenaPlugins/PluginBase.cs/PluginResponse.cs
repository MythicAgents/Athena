namespace PluginBase
{
    public class PluginResponse2
    {
        public string result { get; set; }
        public bool success { get; set; }
    }

    public class PluginResponseError : PluginResponse2 {
        public string errorMessage { get; set; }
    }

}