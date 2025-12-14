using System;
using System.IO;
using System.Text.Json;

namespace SelectML.Client.Services
{
    public class AppConfig
    {
        public string WatchDirectory { get; set; }
        public string LastPluginName { get; set; }
        public string ConnectionString { get; set; }

        // Database Connection Fields
        public string DbServer { get; set; } = @"localhost\MLSQLExpress";
        public bool DbUseWindowsAuth { get; set; } = false;
        public string DbUser { get; set; } = "sa";
        public string DbPassword { get; set; } = "Me@sur1ink$alone";
    }

    public class ConfigService
    {
        private readonly string _configPath = "appsettings.json";

        public void Save(AppConfig config)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(_configPath, json);
        }

        public AppConfig Load()
        {
            if (!File.Exists(_configPath))
                return new AppConfig();

            try
            {
                string json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppConfig>(json);
            }
            catch
            {
                return new AppConfig();
            }
        }
    }
}
