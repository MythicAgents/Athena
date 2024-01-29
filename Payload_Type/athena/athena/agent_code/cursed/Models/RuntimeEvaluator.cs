using Agent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace cursed.Models
{
    public class RuntimeEvaluator
    {
        public int id { get; set; }
        public string method { get; set; }
        public RuntimeEvaluatorParams @params {get; set;}
        public RuntimeEvaluator(string jscode) {
            this.@params = new RuntimeEvaluatorParams(jscode);
            this.id = 1;
            this.method = "Runtime.evaluate";
        }
        public string toJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }
    public class RuntimeEvaluatorParams
    {
        public string expression { get; set; }
        public bool returnByValue { get; set; }

        public RuntimeEvaluatorParams(string expression)
        {
            this.expression = expression;
            this.returnByValue = true;
        }
    }
    [JsonSerializable(typeof(RuntimeEvaluator))]
    public partial class RuntimeEvaluatorJsonContext : JsonSerializerContext
    {
    }
    [JsonSerializable(typeof(RuntimeEvaluatorParams))]
    public partial class RuntimeEvaluatorParamsJsonContext : JsonSerializerContext
    {
    }
}
