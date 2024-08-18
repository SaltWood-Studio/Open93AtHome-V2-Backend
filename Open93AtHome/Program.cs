using Newtonsoft.Json;
using Open93AtHome.Modules;
using YamlDotNet.Serialization;

namespace Open93AtHome
{
    internal class Program
    {
        static void Main(string[] args)
        {
            const string configPath = "config.json";

            Config config;
            if (File.Exists(configPath))
            {
                config = (JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath))) ?? new Config();
            }
            else config = new Config();
            File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));

            BackendServer server = new BackendServer(config);
            server.Start();
        }
    }
}
