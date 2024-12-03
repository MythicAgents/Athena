﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Agent.Profiles.Websocket
{
    public class WebSocketMessage
    {
        public bool client { get; set; }
        public string data { get; set; } = string.Empty;
        public string tag { get; set; } = string.Empty;
    }
    [JsonSerializable(typeof(WebSocketMessage))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    public partial class WebsocketJsonContext : JsonSerializerContext
    {
    }
}
