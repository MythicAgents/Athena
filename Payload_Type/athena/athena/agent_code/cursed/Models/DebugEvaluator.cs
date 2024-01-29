using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace cursed.Models
{
    public class DebugEvaluator
    {
        public int id { get; set; }
        public string method { get; set; }
        public DebugEvaluator(string method)
        {
            this.method = method;
        }

        public string toJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
