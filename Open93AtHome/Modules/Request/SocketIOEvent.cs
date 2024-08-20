using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Open93AtHome.Modules.Request
{
    public class SocketIOEvent
    {
        [JsonProperty("session_id")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("data")]
        public Dictionary<string, object> Data { get; set; } = new();
    }
}
