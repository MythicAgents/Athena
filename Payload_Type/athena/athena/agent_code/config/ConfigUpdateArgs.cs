﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace config
{
    public class ConfigUpdateArgs
    {
        public int sleep { get; set; } = -1;
        public int jitter { get; set; } = -1;
        public string killdate { get; set; } = "01/01/0001";
    }
}