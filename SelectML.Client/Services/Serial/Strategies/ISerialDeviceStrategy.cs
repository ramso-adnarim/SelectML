using SelectML.Client.Services.Serial.Models;

namespace SelectML.Client.Services.Serial.Strategies
{
    public interface ISerialDeviceStrategy
    {
        string Name { get; }
        SerialPortConfig GetConfiguration();
        SerialMeasurement? Parse(string rawData);
    }
}
