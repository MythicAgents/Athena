using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Models
{
    public class GetTasking
    {
        public string action { get; set; }
        public int tasking_size { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ServerDatagram> socks { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ServerDatagram> rpfwd { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<DelegateMessage> delegates { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<InteractMessage> interactive { get; set; }
        [JsonConverter(typeof(UnsafeRawJsonConverter))]
        public List<string> responses { get; set; }
    }


    [JsonSerializable(typeof(GetTasking))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(bool))]
    public partial class GetTaskingJsonContext : JsonSerializerContext
    {
    }
    /// <summary>
    /// Serializes the contents of a string value as raw JSON.  The string is validated as being an RFC 8259-compliant JSON payload
    /// </summary>
    public class RawJsonConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            List<string> ret = new List<string>();
            foreach(var d in doc.RootElement.EnumerateArray())
            {
                ret.Add(d.GetRawText());
            }

            return ret;
        }

        protected virtual bool SkipInputValidation => false;
        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach(var d in value)
            {
                writer.WriteRawValue(d, skipInputValidation: SkipInputValidation);
            }
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// Serializes the contents of a string value as raw JSON.  The string is NOT validated as being an RFC 8259-compliant JSON payload
    /// </summary>
    public class UnsafeRawJsonConverter : RawJsonConverter
    {
        protected override bool SkipInputValidation => true;
    }
}
