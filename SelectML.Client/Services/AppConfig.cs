using System;
using System.IO;
using System.Text.Json;

namespace SelectML.Client.Services
{
    public class AppConfig
    {
        public string WatchDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public bool UseOutputDirectory { get; set; } = false;
        public string LastPluginName { get; set; }
        public string ConnectionString { get; set; }

        // Database Connection Fields
        public string DbServer { get; set; } = @"localhost\MLSQLExpress";
        public bool DbUseWindowsAuth { get; set; } = false;
        public string DbUser { get; set; } = "sa";
        public string DbPassword { get; set; } = "Me@sur1ink$alone";
        public string DbName { get; set; } = "MeasurLink10";

        // Governance
        public int DataRetentionDays { get; set; } = 30;

        // Updates
        public string UpdateUrl { get; set; } = "https://github.com/ramso-adnarim/SelectML/";

        // Serial Persistence
        public string LastSerialPort { get; set; }
        public string LastSerialStrategy { get; set; }
        public string LastSerialFeatureName { get; set; }

        // Auto-Start Persistence
        public bool AutoStartDatabase { get; set; } = false;
        public bool AutoStartSerial { get; set; } = false;

        // Name Modifier
        public string NameModifierMode { get; set; } = "Disabled";
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
