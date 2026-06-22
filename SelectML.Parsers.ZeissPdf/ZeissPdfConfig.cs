using System;
using System.IO;
using System.Text.Json;

namespace SelectML.Parsers.ZeissPdf
{
    public class ZeissPdfConfig
    {
        public string PartNameLabel { get; set; } = "Part name";
        public string BatchNumberLabel { get; set; } = "Run";

        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ZeissPdfConfig.json");

        public static ZeissPdfConfig Load()
        {
            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = new ZeissPdfConfig();
                Save(defaultConfig);
                return defaultConfig;
            }

            try
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<ZeissPdfConfig>(json) ?? new ZeissPdfConfig();
            }
            catch
            {
                return new ZeissPdfConfig();
            }
        }

        public static void Save(ZeissPdfConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
