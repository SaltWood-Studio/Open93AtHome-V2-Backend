using Newtonsoft.Json;
using Open93AtHome.Modules;
using YamlDotNet.Serialization;

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
                Deserializer deserializer = new Deserializer();
                config = (deserializer.Deserialize(File.ReadAllText(configPath)) as Config) ?? new Config();
            }
            else config = new Config();
            Serializer serializer = new Serializer();
            File.WriteAllText(configPath, serializer.Serialize(config));

            BackendServer server = new BackendServer(config);
        }
    }
}
