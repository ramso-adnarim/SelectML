using System;
using System.IO.Ports;
using System.Text.RegularExpressions;
using SelectML.Client.Services.Serial.Models;

namespace SelectML.Client.Services.Serial.Strategies
{
    public class GenericStrategy : ISerialDeviceStrategy
    {
        public string Name => "Genérico";

        public SerialPortConfig GetConfiguration()
        {
            return new SerialPortConfig
            {
                BaudRate = 9600,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One
            };
        }

        public SerialMeasurement? Parse(string rawData)
        {
            if (string.IsNullOrWhiteSpace(rawData)) return null;

            // Capture first valid number (integer or decimal)
            // Supports dot or comma as separator, normalized to dot
            var match = Regex.Match(rawData, @"([+\-]?\d+([.,]\d+)?)");
            
            if (match.Success)
            {
                string cleanVal = match.Value.Replace(',', '.');
                if (double.TryParse(cleanVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    return new SerialMeasurement
                    {
                        Value = val,
                        FeatureName = "Selecione...",
                        IsGeneric = true,
                        Timestamp = DateTime.Now
                    };
                }
            }
            return null;
        }
    }
}
