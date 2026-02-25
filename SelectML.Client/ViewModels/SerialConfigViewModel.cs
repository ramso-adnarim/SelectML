using SelectML.Client.MVVM;
using SelectML.Client.Services.Serial;
using SelectML.Client.Services.Serial.Strategies;
using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using SelectML.Client.Services;

namespace SelectML.Client.ViewModels
{
    public class SerialConfigViewModel : INotifyPropertyChanged
    {
        private string _selectedPort;
        private ISerialDeviceStrategy _selectedStrategy;
        private string _defaultFeatureName;
        private string _connectionStatus = "Desconectado";
        private System.Windows.Media.Brush _connectionStatusBrush = System.Windows.Media.Brushes.Gray;
        private readonly ConfigService _configService;

        public ObservableCollection<string> AvailablePorts { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<ISerialDeviceStrategy> AvailableStrategies { get; set; } = new ObservableCollection<ISerialDeviceStrategy>();

        public RelayCommand ConnectCommand { get; }
        public RelayCommand DisconnectCommand { get; }
        public RelayCommand OpenJsonConfigCommand { get; }

        public SerialConfigViewModel()
        {
            _configService = new ConfigService();
            LoadStrategies();
            LoadPorts();

            // Load Persistence
            var config = _configService.Load();
            if (!string.IsNullOrEmpty(config.LastSerialPort) && AvailablePorts.Contains(config.LastSerialPort))
            {
                SelectedPort = config.LastSerialPort;
            }

            if (!string.IsNullOrEmpty(config.LastSerialStrategy))
            {
                 var strategy = System.Linq.Enumerable.FirstOrDefault(AvailableStrategies, s => s.GetType().Name == config.LastSerialStrategy);
                 if (strategy != null) SelectedStrategy = strategy;
            }

            if (!string.IsNullOrEmpty(config.LastSerialFeatureName))
            {
                DefaultFeatureName = config.LastSerialFeatureName;
            }

            ConnectCommand = new RelayCommand(ExecuteConnect, CanConnect);
            DisconnectCommand = new RelayCommand(ExecuteDisconnect, CanDisconnect);
            OpenJsonConfigCommand = new RelayCommand(ExecuteOpenJson);

            UpdateStatus();
        }

        private void LoadStrategies()
        {
            AvailableStrategies.Add(new UWaveStrategy());
            AvailableStrategies.Add(new GenericStrategy());
            AvailableStrategies.Add(new CustomSerialStrategy());

            SelectedStrategy = AvailableStrategies[0];
        }

        public void LoadPorts()
        {
            AvailablePorts.Clear();
            var ports = SerialPortService.Instance.GetAvailablePorts();
            foreach (var p in ports) AvailablePorts.Add(p);

            if (AvailablePorts.Count > 0) SelectedPort = AvailablePorts[0];
        }

        public string SelectedPort
        {
            get => _selectedPort;
            set { _selectedPort = value; OnPropertyChanged(); }
        }

        public ISerialDeviceStrategy SelectedStrategy
        {
            get => _selectedStrategy;
            set
            {
                _selectedStrategy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomStrategySelected));
                
                // If reselecting custom, reload its config?
                if (value is CustomSerialStrategy custom)
                {
                    custom.LoadConfig();
                }
            }
        }

        public bool IsCustomStrategySelected => SelectedStrategy is CustomSerialStrategy;

        public string DefaultFeatureName
        {
            get => _defaultFeatureName;
            set { _defaultFeatureName = value; OnPropertyChanged(); }
        }

        public bool IsConnected => SerialPortService.Instance.IsConnected;
        public bool IsDisconnected => !IsConnected;

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

        public System.Windows.Media.Brush ConnectionStatusBrush
        {
            get => _connectionStatusBrush;
            set { _connectionStatusBrush = value; OnPropertyChanged(); }
        }

        private void ExecuteConnect(object obj)
        {
            if (string.IsNullOrEmpty(SelectedPort) || SelectedStrategy == null) return;

            try
            {
                // Persistence
                var config = _configService.Load();
                config.LastSerialPort = SelectedPort;
                config.LastSerialStrategy = SelectedStrategy.GetType().Name;
                config.LastSerialFeatureName = DefaultFeatureName;
                config.AutoStartSerial = true; // Habilita início automático se conectar com sucesso
                _configService.Save(config);

                // Check for Hot-Reload if Custom
                if (SelectedStrategy is CustomSerialStrategy custom)
                {
                    // Force reload from disk in case user just edited it
                    custom.LoadConfig(); 
                }

                SerialPortService.Instance.Connect(SelectedPort, SelectedStrategy);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao conectar: {ex.Message}");
            }
        }

        private void ExecuteDisconnect(object obj)
        {
            SerialPortService.Instance.Disconnect();
            
            // Desabilita início automático no desconectar manual
            var config = _configService.Load();
            config.AutoStartSerial = false;
            _configService.Save(config);

            UpdateStatus();
        }

        private void ExecuteOpenJson(object obj)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom_device_config.json");
                if (!File.Exists(path))
                {
                     // Write default if missing
                     File.WriteAllText(path, "{}"); // Should assume default content logic elsewhere but ok
                }
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao abrir arquivo: {ex.Message}");
            }
        }

        private bool CanConnect(object obj) => IsDisconnected && !string.IsNullOrEmpty(SelectedPort);
        private bool CanDisconnect(object obj) => IsConnected;

        private void UpdateStatus()
        {
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(IsDisconnected));
            
            if (IsConnected)
            {
                ConnectionStatus = "CONECTADO";
                ConnectionStatusBrush = System.Windows.Media.Brushes.Green;
            }
            else
            {
                ConnectionStatus = "DESCONECTADO";
                ConnectionStatusBrush = System.Windows.Media.Brushes.Gray;
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
