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
        public event EventHandler<bool>? ConnectionStatusChanged;

        /// <summary>
        /// Construtor privado para garantir o padrão Singleton.
        /// </summary>
        private SerialPortService() { }

        /// <summary>
        /// Retorna a lista de nomes de portas COM disponíveis no sistema (ex: "COM1", "COM3").
        /// </summary>
        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// Abre a conexão serial com a porta especificada e configura a estratégia de parsing.
        /// </summary>
        /// <param name="portName">Nome da porta (ex: "COM1")</param>
        /// <param name="strategy">Estratégia de interpretação dos dados (ex: U-WAVE ou Custom)</param>
        public void Connect(string portName, ISerialDeviceStrategy strategy)
        {
            Disconnect(); // Garante estado limpo antes de conectar

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

                // Assina o evento de recebimento de dados
                _serialPort.DataReceived += OnDataReceived;
                
                // Configura sinais de controle de hardware
                _serialPort.DtrEnable = config.DtrEnable;
                _serialPort.RtsEnable = config.RtsEnable;

                _serialPort.Open();
                _serialPort.DiscardInBuffer(); // Limpa lixo antigo do buffer da porta
                
                ConnectionStatusChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Falha ao conectar na porta {portName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Fecha a conexão serial e libera recursos.
        /// </summary>
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
                catch { /* Ignora erros de fechamento */ }
                finally
                {
                    _serialPort = null;
                }
                ConnectionStatusChanged?.Invoke(this, false);
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

        /// <summary>
        /// Processa o buffer acumulado, extraindo linhas completas e delegando para a estratégia atual.
        /// </summary>
        private void ProcessBuffer()
        {
            string content = _buffer.ToString();
            
            // Lida com \r e \n como delimitadores de quebra de linha
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
                
                // Remove linha + terminador do buffer acumulado
                _buffer.Remove(0, idx + 1);
                content = _buffer.ToString();

                if (!string.IsNullOrWhiteSpace(line))
                {
                    // Lógica de Parse delegada para a estratégia
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
