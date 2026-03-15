namespace http_request
{
    public class HttpRequestArgs
    {
        public string url { get; set; } = "";
        public string method { get; set; } = "GET";
        public string body { get; set; } = "";
        public string headers { get; set; } = "";
        public string cookies { get; set; } = "";
        public int timeout { get; set; } = 30;
        public bool follow_redirects { get; set; } = true;
    }
}
