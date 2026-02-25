using System;
using System.IO.Ports;
using System.Text.Json.Serialization;

namespace SelectML.Client.Services.Serial.Models
{
    public class SerialPortConfig
    {
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Parity Parity { get; set; } = Parity.None;
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StopBits StopBits { get; set; } = StopBits.One;

        public bool DtrEnable { get; set; } = false;
        public bool RtsEnable { get; set; } = false;
    }

    public class DataProtocol
    {
        public string Terminator { get; set; } = "\r";
        public string ExtractionRegex { get; set; } = "";
        public string TargetFeatureName { get; set; } = "";
    }

    public class CustomDeviceConfig
    {
        public SerialPortConfig PortConfig { get; set; } = new SerialPortConfig();
        public DataProtocol DataProtocol { get; set; } = new DataProtocol();
    }

    public class SerialMeasurement
    {
        public double Value { get; set; }
        public string FeatureName { get; set; } = string.Empty;
        public bool IsGeneric { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
