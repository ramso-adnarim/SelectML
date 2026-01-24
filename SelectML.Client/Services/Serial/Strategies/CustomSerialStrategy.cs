using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using SelectML.Client.Services.Serial.Models;

namespace SelectML.Client.Services.Serial.Strategies
{
    public class CustomSerialStrategy : ISerialDeviceStrategy
    {
        public string Name => "Customizado";
        private CustomDeviceConfig _config;
        private const string ConfigFileName = "custom_device_config.json";

        public CustomSerialStrategy()
        {
            LoadConfig();
        }

        public void LoadConfig()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _config = JsonSerializer.Deserialize<CustomDeviceConfig>(json) ?? new CustomDeviceConfig();
                }
                else
                {
                    _config = new CustomDeviceConfig(); 
                }
            }
            catch (Exception)
            {
                // In a real app, log this error. For now, fallback to defaults.
                _config = new CustomDeviceConfig();
            }
        }

        public SerialPortConfig GetConfiguration()
        {
            return _config.PortConfig;
        }

        public SerialMeasurement? Parse(string rawData)
        {
            if (string.IsNullOrWhiteSpace(rawData)) return null;

            try
            {
                string pattern = _config.DataProtocol.ExtractionRegex;
                // If no regex provided, fallback to a generic number finder
                if (string.IsNullOrEmpty(pattern)) 
                {
                    pattern = @"([+\-]?\d+([.,]\d+)?)";
                }

                var match = Regex.Match(rawData, pattern);
                if (match.Success)
                {
                    // Prefer the first capturing group if available, otherwise the full match
                    string valStr = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                    
                    // Normalize to dot decimal
                    valStr = valStr.Replace(',', '.');

                    if (double.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                    {
                        string feature = _config.DataProtocol.TargetFeatureName;
                        bool isGeneric = string.IsNullOrWhiteSpace(feature);
                        if (isGeneric) feature = "Selecione...";

                        return new SerialMeasurement
                        {
                            Value = val,
                            FeatureName = feature,
                            IsGeneric = isGeneric,
                            Timestamp = DateTime.Now
                        };
                    }
                }
            }
            catch
            {
                // Regex errors or parsing errors ignored
            }
            return null;
        }
    }
}
