using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SelectML.Client.MVVM;
using SelectML.Client.Services;
using SelectML.Core;

namespace SelectML.Client.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly PluginLoader _pluginLoader;
        private readonly ConfigService _configService;
        private FileSystemWatcher _watcher;

        // Propriedades de Estado da UI
        private bool _isConfigLocked;
        private string _configButtonText = "Salvar e Iniciar";
        private bool _isExpanded = true;
        private string _statusMessage = "Aguardando configuração...";

        // Dados
        private string _watchDirectory;
        private IMachineParser _selectedParser;
        private string _partName;
        private string _batchNumber;

        public MainViewModel()
        {
            _pluginLoader = new PluginLoader();
            _configService = new ConfigService();
            AvailableParsers = new ObservableCollection<IMachineParser>();
            MeasuredResults = new ObservableCollection<ResultItem>();

            // Comandos
            SelectDirectoryCommand = new RelayCommand(ExecuteSelectDirectory, CanChangeConfig);
            SaveConfigCommand = new RelayCommand(ExecuteSaveConfig);

            // Comandos manuais mantidos para teste
            SendCommand = new RelayCommand(ExecuteSend, CanSend);
            CancelCommand = new RelayCommand(ExecuteCancel);

            LoadParsers();
            LoadConfiguration();
        }

        // --- Propriedades de UI ---

        public ObservableCollection<IMachineParser> AvailableParsers { get; set; }
        public ObservableCollection<ResultItem> MeasuredResults { get; set; }

        public bool IsConfigLocked
        {
            get => _isConfigLocked;
            set
            {
                _isConfigLocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsConfigEnabled));
            }
        }

        public bool IsConfigEnabled => !IsConfigLocked;

        public string ConfigButtonText
        {
            get => _configButtonText;
            set { _configButtonText = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string WatchDirectory
        {
            get => _watchDirectory;
            set { _watchDirectory = value; OnPropertyChanged(); }
        }

        public IMachineParser SelectedParser
        {
            get => _selectedParser;
            set { _selectedParser = value; OnPropertyChanged(); }
        }

        public string PartName
        {
            get => _partName;
            set { _partName = value; OnPropertyChanged(); }
        }

        public string BatchNumber
        {
            get => _batchNumber;
            set { _batchNumber = value; OnPropertyChanged(); }
        }

        // --- Comandos ---
        public RelayCommand SelectDirectoryCommand { get; }
        public RelayCommand SaveConfigCommand { get; }
        public RelayCommand SendCommand { get; }
        public RelayCommand CancelCommand { get; }

        // --- Métodos de Configuração e Watcher ---

        private void LoadParsers()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            var plugins = _pluginLoader.LoadPlugins(path);
            AvailableParsers.Clear();
            foreach (var parser in plugins) AvailableParsers.Add(parser);
        }

        private void LoadConfiguration()
        {
            var config = _configService.Load();
            if (!string.IsNullOrEmpty(config.WatchDirectory))
                WatchDirectory = config.WatchDirectory;

            if (!string.IsNullOrEmpty(config.LastPluginName))
            {
                SelectedParser = AvailableParsers.FirstOrDefault(p => p.MachineName == config.LastPluginName);
            }

            if (SelectedParser == null && AvailableParsers.Count > 0)
                SelectedParser = AvailableParsers[0];
        }

        private void ExecuteSaveConfig(object obj)
        {
            if (IsConfigLocked)
            {
                StopWatcher();
                IsConfigLocked = false;
                ConfigButtonText = "Salvar e Iniciar";
                StatusMessage = "Monitoramento pausado. Edite as configurações.";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(WatchDirectory) || !Directory.Exists(WatchDirectory))
                {
                    System.Windows.MessageBox.Show("Selecione um diretório válido.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (SelectedParser == null)
                {
                    System.Windows.MessageBox.Show("Selecione um plugin de máquina.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var config = new AppConfig
                {
                    WatchDirectory = WatchDirectory,
                    LastPluginName = SelectedParser.MachineName
                };
                _configService.Save(config);

                StartWatcher();

                IsConfigLocked = true;
                ConfigButtonText = "Editar Configuração";
                IsExpanded = false;
            }
        }

        private void StartWatcher()
        {
            try
            {
                if (_watcher != null) StopWatcher();

                _watcher = new FileSystemWatcher(WatchDirectory);
                _watcher.Filter = "*.*";
                _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;

                _watcher.Created += OnFileCreated;

                _watcher.EnableRaisingEvents = true;
                StatusMessage = $"Monitorando: {WatchDirectory} usando {SelectedParser.MachineName}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao iniciar monitoramento: {ex.Message}");
                IsConfigLocked = false;
            }
        }

        private void StopWatcher()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileCreated;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        // --- Lógica de Negócio ---

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (!SelectedParser.CanParse(e.FullPath)) return;

            if (!await WaitForFileAccess(e.FullPath))
            {
                UpdateStatus($"Erro: Arquivo {e.Name} bloqueado ou inacessível.");
                return;
            }

            try
            {
                UpdateStatus($"Processando arquivo: {e.Name}...");

                var data = SelectedParser.Parse(e.FullPath);

                if (data.IsValid)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        PartName = data.PartName;
                        BatchNumber = data.BatchNumber;
                        MeasuredResults.Clear();
                        foreach (var item in data.Results)
                        {
                            MeasuredResults.Add(new ResultItem { Characteristic = item.Key, Value = item.Value });
                        }
                    });

                    GenerateOutputCsv(data);

                    UpdateStatus($"Sucesso! Dados de {data.PartName} processados.");
                }
                else
                {
                    UpdateStatus($"Aviso: Arquivo {e.Name} não contem dados válidos.");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Erro ao processar {e.Name}: {ex.Message}");
            }
        }

        private void GenerateOutputCsv(MeasurementData data)
        {
            try
            {
                string outputDir = Path.Combine(WatchDirectory, "Output");
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                string fileName = $"Result_{data.PartName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullPath = Path.Combine(outputDir, fileName);

                var sb = new StringBuilder();
                sb.AppendLine(data.PartName);
                sb.AppendLine(data.BatchNumber);
                sb.AppendLine(string.Join(",", data.Results.Keys));
                var values = data.Results.Values.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine(string.Join(",", values));

                // CORREÇÃO AQUI: Usar UTF8 com BOM (true) para compatibilidade com Excel
                File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private async Task<bool> WaitForFileAccess(string filePath, int timeoutSeconds = 5)
        {
            int retries = timeoutSeconds * 2;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (stream.Length > 0) return true;
                    }
                }
                catch (IOException)
                {
                    await Task.Delay(500);
                }
            }
            return false;
        }

        private void UpdateStatus(string msg)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => StatusMessage = msg);
        }

        private bool CanChangeConfig(object obj) => IsConfigEnabled;

        private void ExecuteSelectDirectory(object obj)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                WatchDirectory = dlg.SelectedPath;
            }
        }
        private void ExecuteSend(object obj) { }
        private bool CanSend(object obj) => true;
        private void ExecuteCancel(object obj) { }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ResultItem
    {
        public string Characteristic { get; set; }
        public double Value { get; set; }
    }
}