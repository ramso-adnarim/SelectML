using SelectML.Client.MVVM;
using SelectML.Client.Services;
using SelectML.Core;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SelectML.Client.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly PluginLoader _pluginLoader;
        private readonly ConfigService _configService;
        private IDatabaseService _databaseService;
        private FileSystemWatcher _watcher;

        // Propriedades de Estado da UI
        private bool _isConfigLocked;
        private string _configButtonText = "Salvar e Iniciar";
        private bool _isExpanded = true;
        private string _statusMessage = "Aguardando configuração...";
        private bool _isPendingAction;
        private bool _isAutoMode;
        private ImageSource _trayIconSource;

        // Timers
        private System.Windows.Threading.DispatcherTimer _iconTimer;
        private bool _iconToggle;

        // Events
        public event Action<string, string> RequestShowBalloonTip;
        public event Action RequestRestoreWindow;
        public event Action RequestMinimizeWindow;

        // Dados
        private string _watchDirectory;
        private string _connectionString;

        // Database Config Fields
        private string _dbServer = @"localhost\MLSQLExpress";
        private bool _dbUseWindowsAuth = false;
        private string _dbUser = "sa";
        private string _dbPassword = "Me@sur1ink$alone";
        private string _dbName = "SelectML";

        private IMachineParser _selectedParser;
        private string _partName;
        private string _batchNumber;
        private MeasurementData _currentData; // Store current data for processing

        public MainViewModel()
        {
            _pluginLoader = new PluginLoader();
            _configService = new ConfigService();
            AvailableParsers = new ObservableCollection<IMachineParser>();
            MeasuredResults = new ObservableCollection<ResultItem>();
            AvailableDatabases = new ObservableCollection<string>();

            // Initial Icon
            UpdateTrayIcon("Resources/icon_grey.ico");

            // Comandos
            SelectDirectoryCommand = new RelayCommand(ExecuteSelectDirectory, CanChangeConfig);
            SaveConfigCommand = new RelayCommand(ExecuteSaveConfig);
            LoadDatabasesCommand = new RelayCommand(ExecuteLoadDatabases, CanChangeConfig);

            // Comandos de Ação
            SendCommand = new RelayCommand(ExecuteSend, CanExecuteAction);
            CancelCommand = new RelayCommand(ExecuteCancel, CanExecuteAction);

            // Window Commands
            MinimizeToTrayCommand = new RelayCommand(o => RequestMinimizeWindow?.Invoke());
            RestoreFromTrayCommand = new RelayCommand(o => RequestRestoreWindow?.Invoke());

            LoadParsers();
            LoadConfiguration();
        }

        // --- Propriedades de UI ---

        public ObservableCollection<IMachineParser> AvailableParsers { get; set; }
        public ObservableCollection<string> AvailableDatabases { get; set; }
        public ObservableCollection<ResultItem> MeasuredResults { get; set; }

        public bool IsConfigLocked
        {
            get => _isConfigLocked;
            set
            {
                _isConfigLocked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsConfigEnabled));
                OnPropertyChanged(nameof(IsMonitoring));
                OnPropertyChanged(nameof(IsSqlCredentialsEnabled));
            }
        }

        public bool IsConfigEnabled => !IsConfigLocked;

        public bool IsPendingAction
        {
            get => _isPendingAction;
            set
            {
                _isPendingAction = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMonitoring)); // Inverse logic usually helpful for UI
                OnPropertyChanged(nameof(IsSqlCredentialsEnabled));
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CommandManager.InvalidateRequerySuggested();
                });
            }
        }

        public bool IsAutoMode
        {
            get => _isAutoMode;
            set { _isAutoMode = value; OnPropertyChanged(); }
        }

        public ImageSource TrayIconSource
        {
            get => _trayIconSource;
            set { _trayIconSource = value; OnPropertyChanged(); }
        }

        // Helper to check if monitoring is effectively active for UI binding purposes
        public bool IsMonitoring => IsConfigLocked && !IsPendingAction;

        public bool IsSqlCredentialsEnabled => !IsMonitoring && !DbUseWindowsAuth;

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

        public string ConnectionString
        {
            get => _connectionString;
            set { _connectionString = value; OnPropertyChanged(); }
        }

        public string DbServer
        {
            get => _dbServer;
            set
            {
                _dbServer = value;
                OnPropertyChanged();
                BuildConnectionString();
            }
        }

        public bool DbUseWindowsAuth
        {
            get => _dbUseWindowsAuth;
            set
            {
                _dbUseWindowsAuth = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDbAuthEnabled));
                OnPropertyChanged(nameof(IsSqlCredentialsEnabled));
                BuildConnectionString();
            }
        }

        public bool IsDbAuthEnabled => !DbUseWindowsAuth;

        public string DbUser
        {
            get => _dbUser;
            set
            {
                _dbUser = value;
                OnPropertyChanged();
                BuildConnectionString();
            }
        }

        public string DbPassword
        {
            get => _dbPassword;
            set
            {
                _dbPassword = value;
                OnPropertyChanged();
                BuildConnectionString();
            }
        }

        public string DbName
        {
            get => _dbName;
            set
            {
                _dbName = value;
                OnPropertyChanged();
                BuildConnectionString();
            }
        }

        public IMachineParser SelectedParser
        {
            get => _selectedParser;
            set { _selectedParser = value; OnPropertyChanged(); }
        }

        public string PartName
        {
            get => _partName;
            set
            {
                _partName = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string BatchNumber
        {
            get => _batchNumber;
            set
            {
                _batchNumber = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // --- Comandos ---
        public RelayCommand SelectDirectoryCommand { get; }
        public RelayCommand SaveConfigCommand { get; }
        public RelayCommand LoadDatabasesCommand { get; }
        public RelayCommand SendCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand MinimizeToTrayCommand { get; }
        public RelayCommand RestoreFromTrayCommand { get; }

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

            // Load individual DB fields
            DbServer = !string.IsNullOrEmpty(config.DbServer) ? config.DbServer : @"localhost\MLSQLExpress";
            DbUseWindowsAuth = config.DbUseWindowsAuth;
            DbUser = !string.IsNullOrEmpty(config.DbUser) ? config.DbUser : "sa";
            DbPassword = !string.IsNullOrEmpty(config.DbPassword) ? config.DbPassword : "Me@sur1ink$alone";

            // Set DbName separately to avoid triggering BuildConnectionString multiple times unnecessarily,
            // or ensure it defaults correctly.
            _dbName = !string.IsNullOrEmpty(config.DbName) ? config.DbName : "SelectML";
            OnPropertyChanged(nameof(DbName));

            // Populate AvailableDatabases with at least the current one if not empty
            if (!string.IsNullOrEmpty(_dbName)) AvailableDatabases.Add(_dbName);

            // Build connection string from loaded fields
            BuildConnectionString();

            if (!string.IsNullOrEmpty(config.LastPluginName))
            {
                SelectedParser = AvailableParsers.FirstOrDefault(p => p.MachineName == config.LastPluginName);
            }

            if (SelectedParser == null && AvailableParsers.Count > 0)
                SelectedParser = AvailableParsers[0];

            // Initialize Database Service
            _databaseService = new DatabaseService(config.ConnectionString);
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

                var config = _configService.Load(); // Reload to keep existing keys
                config.WatchDirectory = WatchDirectory;
                config.LastPluginName = SelectedParser.MachineName;

                // Save DB fields
                config.DbServer = DbServer;
                config.DbUseWindowsAuth = DbUseWindowsAuth;
                config.DbUser = DbUser;
                config.DbPassword = DbPassword;
                config.DbName = DbName;
                config.ConnectionString = ConnectionString;

                _configService.Save(config);

                // Re-initialize database service
                _databaseService = new DatabaseService(config.ConnectionString);

                StartWatcher();

                IsConfigLocked = true;
                ConfigButtonText = "Editar Configuração";
                IsExpanded = false;
            }
        }

        private async void ExecuteLoadDatabases(object obj)
        {
            try
            {
                StatusMessage = "Listando bancos de dados...";

                // Build a connection string to master
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder();
                builder.DataSource = !string.IsNullOrEmpty(DbServer) ? DbServer : @"localhost\MLSQLExpress";
                builder.InitialCatalog = "master";
                builder.TrustServerCertificate = true;

                if (DbUseWindowsAuth)
                {
                    builder.IntegratedSecurity = true;
                }
                else
                {
                    builder.IntegratedSecurity = false;
                    builder.UserID = !string.IsNullOrEmpty(DbUser) ? DbUser : "sa";
                    builder.Password = !string.IsNullOrEmpty(DbPassword) ? DbPassword : "";
                }

                var dbs = await _databaseService.GetAvailableDatabasesAsync(builder.ConnectionString);

                AvailableDatabases.Clear();
                foreach (var db in dbs)
                {
                    AvailableDatabases.Add(db);
                }

                if (AvailableDatabases.Contains(DbName))
                {
                   // keep selection
                }
                else if (AvailableDatabases.Count > 0)
                {
                    DbName = AvailableDatabases[0];
                }

                StatusMessage = "Lista de bancos de dados atualizada.";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao listar bancos de dados: {ex.Message}", "Erro de Conexão");
                StatusMessage = "Erro ao conectar no banco de dados.";
            }
        }

        private void BuildConnectionString()
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder();
            builder.DataSource = !string.IsNullOrEmpty(DbServer) ? DbServer : @"localhost\MLSQLExpress";
            builder.InitialCatalog = !string.IsNullOrEmpty(DbName) ? DbName : "SelectML";
            builder.TrustServerCertificate = true; // Often needed for local devs

            if (DbUseWindowsAuth)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.IntegratedSecurity = false;
                builder.UserID = !string.IsNullOrEmpty(DbUser) ? DbUser : "sa";
                builder.Password = !string.IsNullOrEmpty(DbPassword) ? DbPassword : "";
            }

            ConnectionString = builder.ConnectionString;
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

                // Start Icon Animation
                _iconTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
                _iconTimer.Tick += (s, e) =>
                {
                    UpdateTrayIcon(_iconToggle ? "Resources/icon_green.ico" : "Resources/icon_grey.ico");
                    _iconToggle = !_iconToggle;
                };
                _iconTimer.Start();
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
            if (_iconTimer != null)
            {
                _iconTimer.Stop();
                _iconTimer = null;
            }
            UpdateTrayIcon("Resources/icon_grey.ico");
        }

        private void UpdateTrayIcon(string resourcePath)
        {
            try
            {
                // Use explicit Pack URI with Assembly Name (SelectML.Client)
                var uri = new Uri($"pack://application:,,,/SelectML.Client;component/{resourcePath}");
                var icon = new BitmapImage(uri);
                icon.Freeze(); // Make it cross-thread accessible if needed
                TrayIconSource = icon;
            }
            catch (Exception)
            {
                // Fallback or log?
            }
        }

        // --- Lógica de Negócio ---

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (IsPendingAction) return;

            if (!SelectedParser.CanParse(e.FullPath)) return;

            if (!await WaitForFileAccess(e.FullPath))
            {
                UpdateStatus($"Erro: Arquivo {e.Name} bloqueado ou inacessível.");
                return;
            }

            try
            {
                UpdateStatus($"Lendo arquivo: {e.Name}...");

                var data = SelectedParser.Parse(e.FullPath);

                if (data.IsValid)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        _currentData = data;

                        PartName = data.PartName;
                        BatchNumber = data.BatchNumber;
                        MeasuredResults.Clear();
                        foreach (var item in data.Results)
                        {
                            MeasuredResults.Add(new ResultItem { Characteristic = item.Key, Value = item.Value });
                        }

                        if (IsAutoMode)
                        {
                            await GenerateOutputCsv(data);
                        }
                        else
                        {
                            IsPendingAction = true;
                            StatusMessage = "Dados carregados. Verifique e clique em Enviar.";
                            RequestRestoreWindow?.Invoke();
                        }
                    });
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

        private bool CanExecuteAction(object obj)
        {
            return IsPendingAction && !string.IsNullOrWhiteSpace(PartName) && !string.IsNullOrWhiteSpace(BatchNumber);
        }

        private async void ExecuteSend(object obj)
        {
            if (_currentData == null) return;
            IsPendingAction = false;
            await GenerateOutputCsv(_currentData);
        }

        private async Task GenerateOutputCsv(MeasurementData data)
        {
            try
            {
                StatusMessage = "Salvando dados...";

                string outputDir = Path.Combine(WatchDirectory, "Output");

                string stationName = await _databaseService.GetStationNameAsync(data.BatchNumber);

                string targetSubDir = !string.IsNullOrWhiteSpace(stationName) ? stationName : "Unidentified";
                string targetPath = Path.Combine(outputDir, targetSubDir);

                if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);

                string fileName = $"Result_{data.PartName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string fullPath = Path.Combine(targetPath, fileName);

                var sb = new StringBuilder();
                sb.AppendLine(data.PartName);
                sb.AppendLine(data.BatchNumber);
                sb.AppendLine(string.Join(",", data.Results.Keys));
                var values = data.Results.Values.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine(string.Join(",", values));

                await File.WriteAllTextAsync(fullPath, sb.ToString(), new UTF8Encoding(true));

                UpdateStatus($"Sucesso! Salvo em {targetSubDir}\\{fileName}");

                if (IsAutoMode)
                {
                    RequestShowBalloonTip?.Invoke("SelectML - Processado", $"Arquivo salvo: {fileName}");
                }

                ResetUI();
            }
            catch (Exception ex)
            {
                IsPendingAction = true;
                UpdateStatus($"Erro ao salvar: {ex.Message}");

                if (IsAutoMode)
                {
                    RequestRestoreWindow?.Invoke();
                }
                else
                {
                    System.Windows.MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteCancel(object obj)
        {
            IsPendingAction = false;
            UpdateStatus("Operação cancelada. Monitoramento retomado.");
            ResetUI();
        }

        private void ResetUI()
        {
            _currentData = null;
            PartName = string.Empty;
            BatchNumber = string.Empty;
            MeasuredResults.Clear();
            // IsPendingAction is already false
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
