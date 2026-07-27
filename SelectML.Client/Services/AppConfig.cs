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
        public string CustomNameModifierFormat { get; set; } = "{N,2,A} {T,3,A}";

        // UI, Theme & Window Geometry Persistence
        public bool IsDarkMode { get; set; } = true;
        public double WindowWidth { get; set; } = 1100;
        public double WindowHeight { get; set; } = 700;
        public double WindowTop { get; set; } = double.NaN;
        public double WindowLeft { get; set; } = double.NaN;
        public int WindowState { get; set; } = 0; // 0 = Normal, 2 = Maximized
    }

    public class ConfigService
    {
        private readonly string _configDirectory;
        private readonly string _configPath;

        public ConfigService()
        {
            _configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SelectML");
            _configPath = Path.Combine(_configDirectory, "appsettings.json");
        }

        public void Save(AppConfig config)
        {
            try
            {
                if (!Directory.Exists(_configDirectory))
                {
                    Directory.CreateDirectory(_configDirectory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to save configuration to {Path}", _configPath);
            }
        }

        public AppConfig Load()
        {
            EnsureMigratedFromLegacyPath();

            if (!File.Exists(_configPath))
                return new AppConfig();

            try
            {
                string json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to load configuration from {Path}", _configPath);
                return new AppConfig();
            }
        }

        private void EnsureMigratedFromLegacyPath()
        {
            if (File.Exists(_configPath)) return;

            try
            {
                string localLegacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                string legacyFoundPath = null;

                if (File.Exists(localLegacyPath))
                {
                    legacyFoundPath = localLegacyPath;
                }
                else
                {
                    // Search in parent Velopack folders (e.g. AppData\Local\SelectML\app-1.2.*\appsettings.json)
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    DirectoryInfo parentDir = Directory.GetParent(baseDir);
                    if (parentDir != null && parentDir.Parent != null)
                    {
                        var appDirs = parentDir.Parent.GetDirectories("app-*");
                        foreach (var dir in appDirs)
                        {
                            string candidate = Path.Combine(dir.FullName, "appsettings.json");
                            if (File.Exists(candidate))
                            {
                                legacyFoundPath = candidate;
                                break;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(legacyFoundPath) && File.Exists(legacyFoundPath))
                {
                    if (!Directory.Exists(_configDirectory))
                    {
                        Directory.CreateDirectory(_configDirectory);
                    }

                    File.Copy(legacyFoundPath, _configPath, overwrite: true);
                    Serilog.Log.Information("Migrated legacy configuration from {Source} to {Destination}", legacyFoundPath, _configPath);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to migrate legacy configuration");
            }
        }
    }
}
