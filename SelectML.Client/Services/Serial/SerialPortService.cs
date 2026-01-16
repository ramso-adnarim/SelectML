using System;
using System.IO.Ports;
using System.Text;
using SelectML.Client.Services.Serial.Models;
using SelectML.Client.Services.Serial.Strategies;

namespace SelectML.Client.Services.Serial
{
    public class SerialPortService
    {
        private static SerialPortService? _instance;
        public static SerialPortService Instance => _instance ??= new SerialPortService();

        private SerialPort? _serialPort;
        private ISerialDeviceStrategy? _currentStrategy;
        private readonly StringBuilder _buffer = new StringBuilder();

        public event EventHandler<SerialMeasurement>? MeasurementReceived;
        public event EventHandler<string>? ErrorReceived;

        private SerialPortService() { }

        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        public void Connect(string portName, ISerialDeviceStrategy strategy)
        {
            Disconnect(); // Ensure clean state

            try
            {
                _currentStrategy = strategy;
                var config = strategy.GetConfiguration();

                _serialPort = new SerialPort(
                    portName,
                    config.BaudRate,
                    config.Parity,
                    config.DataBits,
                    config.StopBits
                );

                // Ensure we read efficiently
                _serialPort.DataReceived += OnDataReceived;
                _serialPort.Open();
                _serialPort.DiscardInBuffer(); // Clear old trash
            }
            catch (Exception ex)
            {
                throw new Exception($"Falha ao conectar na porta {portName}: {ex.Message}", ex);
            }
        }

        public void Disconnect()
        {
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                }
                catch { /* Ignore close errors */ }
                finally
                {
                    _serialPort = null;
                }
            }
            _buffer.Clear();
        }

        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                string data = _serialPort.ReadExisting();
                _buffer.Append(data);

                ProcessBuffer();
            }
            catch (Exception ex)
            {
                ErrorReceived?.Invoke(this, $"Erro na leitura: {ex.Message}");
            }
        }

        private void ProcessBuffer()
        {
            string content = _buffer.ToString();
            
            // Handles both \r and \n as delimiters
            while (true)
            {
                int r = content.IndexOf('\r');
                int n = content.IndexOf('\n');
                int idx = -1;

                if (r != -1 && n != -1) idx = Math.Min(r, n);
                else if (r != -1) idx = r;
                else if (n != -1) idx = n;

                if (idx == -1) break;

                string line = content.Substring(0, idx);
                
                // Remove line + terminator from buffer
                _buffer.Remove(0, idx + 1);
                content = _buffer.ToString();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    // Parse logic
                    if (_currentStrategy != null)
                    {
                        try 
                        {
                            var result = _currentStrategy.Parse(line);
                            if (result != null)
                            {
                                MeasurementReceived?.Invoke(this, result);
                            }
                        }
                        catch (Exception parseEx)
                        {
                             ErrorReceived?.Invoke(this, $"Erro no parse: {parseEx.Message}");
                        }
                    }
                }
            }
        }
    }
}
