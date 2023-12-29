using System.Text.Json.Serialization;

namespace Agent
{
    public class Background
    {
        [JsonPropertyName("page")]
        public string page { get; set; }
    }

    public class BrowserAction
    {
        [JsonPropertyName("default_icon")]
        public DefaultIcon default_icon { get; set; }

        [JsonPropertyName("default_popup")]
        public string default_popup { get; set; }

        [JsonPropertyName("default_title")]
        public string default_title { get; set; }
    }

    public class Commands
    {
        [JsonPropertyName("launch-element-picker")]
        public LaunchElementPicker launchelementpicker { get; set; }

        [JsonPropertyName("launch-element-zapper")]
        public LaunchElementZapper launchelementzapper { get; set; }

        [JsonPropertyName("launch-logger")]
        public LaunchLogger launchlogger { get; set; }

        [JsonPropertyName("open-dashboard")]
        public OpenDashboard opendashboard { get; set; }

        [JsonPropertyName("relax-blocking-mode")]
        public RelaxBlockingMode relaxblockingmode { get; set; }

        [JsonPropertyName("toggle-cosmetic-filtering")]
        public ToggleCosmeticFiltering togglecosmeticfiltering { get; set; }
    }

    public class ContentScript
    {
        [JsonPropertyName("all_frames")]
        public bool all_frames { get; set; }

        [JsonPropertyName("js")]
        public List<string> js { get; set; }

        [JsonPropertyName("match_about_blank")]
        public bool match_about_blank { get; set; }

        [JsonPropertyName("matches")]
        public List<string> matches { get; set; }

        [JsonPropertyName("run_at")]
        public string run_at { get; set; }
    }

    public class DefaultIcon
    {
        [JsonPropertyName("16")]
        public string _16 { get; set; }

        [JsonPropertyName("32")]
        public string _32 { get; set; }

        [JsonPropertyName("64")]
        public string _64 { get; set; }
    }

    public class Icons
    {
        [JsonPropertyName("16")]
        public string _16 { get; set; }

        [JsonPropertyName("32")]
        public string _32 { get; set; }

        [JsonPropertyName("64")]
        public string _64 { get; set; }

        [JsonPropertyName("128")]
        public string _128 { get; set; }
    }

    public class LaunchElementPicker
    {
        [JsonPropertyName("description")]
        public string description { get; set; }
    }

    public class LaunchElementZapper
    {
        [JsonPropertyName("description")]
        public string description { get; set; }
    }

    public class LaunchLogger
    {
        [JsonPropertyName("description")]
        public string description { get; set; }
    }

    public class OpenDashboard
    {
        [JsonPropertyName("description")]
        public string description { get; set; }
    }

    public class OptionsUi
    {
        [JsonPropertyName("open_in_tab")]
        public bool open_in_tab { get; set; }

        [JsonPropertyName("page")]
        public string page { get; set; }
    }

    public class RelaxBlockingMode
    {
        [JsonPropertyName("description")]
        public string description { get; set; }
    }

    public class Result
    {
        [JsonPropertyName("type")]
        public string type { get; set; }

        [JsonPropertyName("value")]
        public Value value { get; set; }
    }

    public class ExtensionManifest
    {
        [JsonPropertyName("result")]
        public Result result { get; set; }
    }

    public class Storage
    {
        [JsonPropertyName("managed_schema")]
        public string managed_schema { get; set; }
    }

    public class ToggleCosmeticFiltering
    {
        [JsonPropertyName("description")]
        public string description { get; set; }
    }

    public class Value
    {
        [JsonPropertyName("author")]
        public string author { get; set; }

        [JsonPropertyName("background")]
        public Background background { get; set; }

        [JsonPropertyName("browser_action")]
        public BrowserAction browser_action { get; set; }

        [JsonPropertyName("commands")]
        public Commands commands { get; set; }

        [JsonPropertyName("content_scripts")]
        public List<ContentScript> content_scripts { get; set; }

        [JsonPropertyName("content_security_policy")]
        public string content_security_policy { get; set; }

        [JsonPropertyName("current_locale")]
        public string current_locale { get; set; }

        [JsonPropertyName("default_locale")]
        public string default_locale { get; set; }

        [JsonPropertyName("description")]
        public string description { get; set; }

        [JsonPropertyName("icons")]
        public Icons icons { get; set; }

        [JsonPropertyName("incognito")]
        public string incognito { get; set; }

        [JsonPropertyName("key")]
        public string key { get; set; }

        [JsonPropertyName("manifest_version")]
        public int manifest_version { get; set; }

        [JsonPropertyName("minimum_chrome_version")]
        public string minimum_chrome_version { get; set; }

        [JsonPropertyName("name")]
        public string name { get; set; }

        [JsonPropertyName("options_ui")]
        public OptionsUi options_ui { get; set; }

        [JsonPropertyName("permissions")]
        public List<string> permissions { get; set; }

        [JsonPropertyName("short_name")]
        public string short_name { get; set; }

        [JsonPropertyName("storage")]
        public Storage storage { get; set; }

        [JsonPropertyName("update_url")]
        public string update_url { get; set; }

        [JsonPropertyName("version")]
        public string version { get; set; }

        [JsonPropertyName("web_accessible_resources")]
        public List<string> web_accessible_resources { get; set; }
    }
}


