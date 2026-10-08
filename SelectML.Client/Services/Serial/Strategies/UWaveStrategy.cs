using System;
using System.IO.Ports;
using System.Text.RegularExpressions;
using SelectML.Client.Services.Serial.Models;

namespace SelectML.Client.Services.Serial.Strategies
{
    public class UWaveStrategy : ISerialDeviceStrategy
    {
        public string Name => "U-WAVE";

        public SerialPortConfig GetConfiguration()
        {
            return new SerialPortConfig
            {
                BaudRate = 57600,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                DtrEnable = true,
                RtsEnable = true
            };
        }

        public bool IsUndoCommand(string rawData)
        {
            if (string.IsNullOrWhiteSpace(rawData)) return false;

            // Remove espaços em branco
            string clean = Regex.Replace(rawData.Trim(), @"\s+", "");

            // Padrão Mitutoyo U-WAVE Cancel/Undo:
            // Começa com ST1, seguido do canal/identificador e finalizado com código de cancelamento 41899
            // Exemplo fornecido: "ST10001000000041899" (onde "01" é o canal do transmissor e pode variar)
            return clean.StartsWith("ST1", StringComparison.OrdinalIgnoreCase) 
                && clean.Contains("41899");
        }

        public SerialMeasurement? Parse(string rawData)
        {
            if (string.IsNullOrWhiteSpace(rawData)) return null;

            // Format: DT10001+00012.34567M
            // Check for DT prefix
            if (!rawData.Contains("DT")) return null;

            // Extract number with sign: +00012.34567
            var match = Regex.Match(rawData, @"([+\-]\d+\.\d+)");
            
            if (match.Success && double.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                return new SerialMeasurement
                {
                    Value = val,
                    FeatureName = "Rugosidade (Ra)",
                    IsGeneric = false,
                    Timestamp = DateTime.Now
                };
            }
            return null;
        }
    }
}
