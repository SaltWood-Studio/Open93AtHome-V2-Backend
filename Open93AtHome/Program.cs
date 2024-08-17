using Newtonsoft.Json;
using Open93AtHome.Modules;

namespace Open93AtHome
{
    internal class Program
    {
        static void Main(string[] args)
        {
            const string configPath = "config.yml";

            Config config;
            if (File.Exists(configPath))
            {
                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath)) ?? new Config();
            }
            else config = new Config();

            BackendServer server = new BackendServer(config);
        }
    }
}
