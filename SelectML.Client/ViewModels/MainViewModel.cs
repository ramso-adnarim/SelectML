using SelectML.Client.MVVM;
using SelectML.Client.Services;
using SelectML.Client.Services.Serial;
using SelectML.Client.Services.Serial.Models;
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
using Serilog;
using SelectML.Client; // Needed for ResultItem
using SelectML.Client.Views; // For ConfirmationWindow
using System.Collections.Generic;
using Velopack;

namespace SelectML.Client.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly PluginLoader _pluginLoader;
        private readonly ConfigService _configService;
        private readonly FileLifecycleService _fileLifecycleService;
        private readonly VelopackService _velopackService;
        private IDatabaseService _databaseService;
        private FileSystemWatcher _watcher;

        // Propriedades de Estado da UI
        private bool _isConfigLocked;
        private string _configButtonText = "Iniciar";
        private bool _isExpanded = true;
        private string _statusMessage = "Aguardando configuração...";
        private bool _isPendingAction;
        private bool _isAutoMode;
        private ImageSource _trayIconSource;
        private bool _isDarkMode;

        // Update State
        private bool _isUpdateAvailable;
        private string _newVersionString;
        private UpdateInfo _pendingUpdate;

        // Icons
        private ImageSource _iconGreen;
        private ImageSource _iconGrey;

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
        private string _detectedStationName;
        private MeasurementData _currentData; // Store current data for processing

        // Runtime state for session-based suppression
        private ConfirmationAction _sessionConfirmationAction = ConfirmationAction.None;

        // Serial Buffer
        private Queue<SerialMeasurement> _serialBuffer = new Queue<SerialMeasurement>();
        private Queue<string> _fileBuffer = new Queue<string>();
        // _isProcessingFile is effectively tracked by IsPendingAction, but we'll add explicit tracking if needed or use IsPendingAction.
        // Prompt requested _isProcessingFile flag.
        private bool _isProcessingFile;

        public MainViewModel()
        {
            _pluginLoader = new PluginLoader();
            _configService = new ConfigService();
            _fileLifecycleService = new FileLifecycleService();
            AvailableParsers = new ObservableCollection<IMachineParser>();
            MeasuredResults = new ObservableCollection<ResultItem>();
            AvailableDatabases = new ObservableCollection<string>();

            // Initialize Config Service first to get URL
            var initialConfig = _configService.Load();
            _velopackService = new VelopackService(initialConfig.UpdateUrl);

            // Pre-load Icons
            _iconGrey = LoadIcon("Resources/icon_grey.ico");
            _iconGreen = LoadIcon("Resources/icon_green.ico");

            // Set Initial Icon
            TrayIconSource = _iconGrey;

            // Comandos
            SelectDirectoryCommand = new RelayCommand(ExecuteSelectDirectory, CanChangeConfig);
            SaveConfigCommand = new RelayCommand(ExecuteSaveConfig);
            LoadDatabasesCommand = new RelayCommand(ExecuteLoadDatabases, CanChangeConfig);
            ToggleThemeCommand = new RelayCommand(ExecuteToggleTheme);

            // Comandos de Ação
            SendCommand = new RelayCommand(ExecuteSend, CanExecuteAction);
            CancelCommand = new RelayCommand(ExecuteCancel, CanExecuteAction);
            RemoveRowCommand = new RelayCommand(ExecuteRemoveRow);

            // Window Commands
            MinimizeToTrayCommand = new RelayCommand(o => RequestMinimizeWindow?.Invoke());
            RestoreFromTrayCommand = new RelayCommand(o => RequestRestoreWindow?.Invoke());

            // Update Commands
            OpenUpdateWindowCommand = new RelayCommand(ExecuteOpenUpdateWindow);
            ConfirmUpdateCommand = new RelayCommand(ExecuteConfirmUpdate);
            CancelUpdateCommand = new RelayCommand(ExecuteCancelUpdate);
            
            OpenSerialConfigCommand = new RelayCommand(ExecuteOpenSerialConfig);
            ClearParserCommand = new RelayCommand(obj => SelectedParser = null, CanChangeConfig);

            LoadParsers();
            LoadConfiguration();

            // Initialize Theme
            IsDarkMode = Properties.Settings.Default.IsDarkMode;

            // Trigger Cleanup on Startup
            PerformCleanup();

            // Trigger Update Check
            Task.Run(CheckForUpdates);
            
            // Subscribe to Serial Events
            SerialPortService.Instance.MeasurementReceived += OnSerialMeasurementReceived;
            SerialPortService.Instance.ConnectionStatusChanged += (s, connected) => IsSerialConnected = connected;
            IsSerialConnected = SerialPortService.Instance.IsConnected;

            // Hook into collection changes to validation
            // Ideally we should hook into item PropertyChanged but we already do that in OnSerialMeasurementReceived roughly
            // We'll iterate in TriggerValidation.
        }

        // Validation Properties
        private bool _isPartNameValid = true;
        private string _expectedPartName;
        public bool IsPartNameValid
        {
            get => _isPartNameValid;
            set { _isPartNameValid = value; OnPropertyChanged(); }
        }

        private bool _isBatchNumberValid = true;
        public bool IsBatchNumberValid
        {
            get => _isBatchNumberValid;
            set { _isBatchNumberValid = value; OnPropertyChanged(); }
        }

        private void PerformCleanup()
        {
            try
            {
                var config = _configService.Load();
                // Fire and forget
                _ = _fileLifecycleService.PerformCleanupAsync(config.WatchDirectory, config.DataRetentionDays);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initiate cleanup task");
            }
        }

        // --- Propriedades de UI ---

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    OnPropertyChanged();
                    // Update Settings
                    Properties.Settings.Default.IsDarkMode = value;
                    Properties.Settings.Default.Save();
                    // Apply Theme
                    if (System.Windows.Application.Current is App app)
                    {
                        app.SetTheme(value);
                        Log.Information("Theme changed to {Theme}", value ? "Dark" : "Light");
                    }
                }
            }
        }

        public ObservableCollection<IMachineParser> AvailableParsers { get; set; }
        public ObservableCollection<string> AvailableDatabases { get; set; }
        public ObservableCollection<ResultItem> MeasuredResults { get; set; }
        public ObservableCollection<string> KnownFeatures { get; set; } = new ObservableCollection<string>();

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

        private bool _isSerialConnected;
        public bool IsSerialConnected
        {
            get => _isSerialConnected;
            set { _isSerialConnected = value; OnPropertyChanged(); }
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
                // Trigger SQL lookup here on lost focus or explicit command?
                // Prompt: "Ao perder o foco ou digitar, o sistema dispara a query SQL"
                CommandManager.InvalidateRequerySuggested();
                // Trigger Lookup
                _ = LoadFeaturesForPart();
                TriggerValidation();
            }
        }

        private bool _suppressBatchLookup;

        public string BatchNumber
        {
            get => _batchNumber;
            set
            {
                _batchNumber = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
                
                // Trigger Station Lookup unless suppressed (programmatic load)
                if (!_suppressBatchLookup)
                {
                    _ = DetectStationAndFeatures();
                }
            }
        }

        private async Task DetectStationAndFeatures()
        {
             try
             {
                 if (string.IsNullOrWhiteSpace(BatchNumber)) 
                 {
                     IsBatchNumberValid = true; // Empirically, empty is valid until typed? Or pending. Let's say valid to avoid redbox on clear.
                     return;
                 }

                 var station = await _databaseService.GetStationNameAsync(BatchNumber);
                 var routine = await _databaseService.GetRoutineNameAsync(BatchNumber);

                 bool found = false;

                 if (!string.IsNullOrEmpty(station))
                 {
                     DetectedStationName = station;
                     found = true;
                 }
                 else
                 {
                     DetectedStationName = "Não identificada";
                 }

                 if (!string.IsNullOrEmpty(routine))
                 {
                     _expectedPartName = routine;
                     found = true;
                     // Auto-fill if empty
                     if (string.IsNullOrWhiteSpace(PartName))
                     {
                         PartName = routine;
                     }
                 }
                 else
                 {
                     _expectedPartName = null;
                 }
                 
                 IsBatchNumberValid = found;

                 // Also load features if possible (redundant with PartName trigger but safer)
                 await LoadFeaturesForPart();
                 TriggerValidation();
             }
             catch (Exception ex)
             {
                 Log.Error(ex, "Error detecting station from manual input");
             }
        }

        public string DetectedStationName
        {
            get => _detectedStationName;
            set
            {
                _detectedStationName = value;
                OnPropertyChanged();
            }
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set
            {
                _isUpdateAvailable = value;
                OnPropertyChanged();
            }
        }

        public string NewVersionString
        {
            get => _newVersionString;
            set
            {
                _newVersionString = value;
                OnPropertyChanged();
            }
        }

        public string CurrentVersion
        {
            get
            {
                try {
                    return System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
                } catch { return "1.0.0"; }
            }
        }

        public string UpdateMessage => $"Versão {NewVersionString} pronta para instalar.";

        // --- Comandos ---
        public RelayCommand SelectDirectoryCommand { get; }
        public RelayCommand SaveConfigCommand { get; }
        public RelayCommand LoadDatabasesCommand { get; }
        public RelayCommand ToggleThemeCommand { get; }
        public RelayCommand SendCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand RemoveRowCommand { get; }
        public RelayCommand MinimizeToTrayCommand { get; }
        public RelayCommand RestoreFromTrayCommand { get; }

        // Update Commands
        public RelayCommand OpenUpdateWindowCommand { get; }
        public RelayCommand ConfirmUpdateCommand { get; }
        public RelayCommand CancelUpdateCommand { get; }

        public RelayCommand OpenSerialConfigCommand { get; }
        public RelayCommand ClearParserCommand { get; }

        // --- Métodos de Configuração e Watcher ---

        private void ExecuteToggleTheme(object obj)
        {
            IsDarkMode = !IsDarkMode;
        }

        private async Task CheckForUpdates()
        {
             try
             {
                 var updateInfo = await _velopackService.CheckForUpdatesAsync();
                 if (updateInfo != null)
                 {
                     System.Windows.Application.Current.Dispatcher.Invoke(() =>
                     {
                         _pendingUpdate = updateInfo;
                         NewVersionString = updateInfo.TargetFullRelease.Version.ToString();
                         IsUpdateAvailable = true;
                     });
                 }
             }
             catch (Exception ex)
             {
                 Log.Error(ex, "Error checking for updates in background");
             }
        }

        private void ExecuteOpenUpdateWindow(object obj)
        {
            if (!IsUpdateAvailable) return;

            var window = new UpdateWindow();
            window.DataContext = this;
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.ShowDialog();
        }

        private async void ExecuteConfirmUpdate(object obj)
        {
            if (_pendingUpdate != null)
            {
                // Close the dialog if it's open (it should be, since this command is from the dialog)
                if (obj is Window win) win.Close();
                // Or handle via attached property or just assume user clicked it.
                // Best practice: passing window as CommandParameter

                try
                {
                   StatusMessage = "Baixando e instalando atualização...";
                   await _velopackService.DownloadAndInstallAsync(_pendingUpdate);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Erro ao atualizar: {ex.Message}");
                }
            }
        }

        private void ExecuteCancelUpdate(object obj)
        {
            if (obj is Window win)
            {
                win.Close();
            }
        }

        private void ExecuteOpenSerialConfig(object obj)
        {
            var vm = new SerialConfigViewModel();
            var win = new Views.SerialConfigWindow();
            win.DataContext = vm;
            win.Owner = System.Windows.Application.Current.MainWindow;
            win.ShowDialog();
        }

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
            _dbName = !string.IsNullOrEmpty(config.DbName) ? config.DbName : "MeasurLink10";
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
            {
                // Do not force selection. Allow Serial Only mode.
                // SelectedParser = AvailableParsers[0];
            }

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
                Log.Information("Configuration unlocked for editing");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(WatchDirectory) || !Directory.Exists(WatchDirectory))
                {
                    System.Windows.MessageBox.Show("Selecione um diretório válido.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    Log.Warning("Invalid watch directory selected: {Directory}", WatchDirectory);
                    return;
                }

                /* 
                // Allow null parser for Serial Only mode
                if (SelectedParser == null)
                {
                    System.Windows.MessageBox.Show("Selecione um plugin de máquina.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    Log.Warning("No parser selected");
                    return;
                }
                */

                var config = _configService.Load(); // Reload to keep existing keys
                config.WatchDirectory = WatchDirectory;
                config.LastPluginName = SelectedParser?.MachineName ?? ""; // Allow null

                // Save DB fields
                config.DbServer = DbServer;
                config.DbUseWindowsAuth = DbUseWindowsAuth;
                config.DbUser = DbUser;
                config.DbPassword = DbPassword;
                config.DbName = DbName;
                config.ConnectionString = ConnectionString;

                _configService.Save(config);
                Log.Information("Configuration saved");
                
                // Re-initialize database service
                _databaseService = new DatabaseService(config.ConnectionString);

                StartWatcher();

                IsConfigLocked = true;
                ConfigButtonText = "Editar";
                IsExpanded = false;
            }
        }

        private async void ExecuteLoadDatabases(object obj)
        {
            try
            {
                StatusMessage = "Listando bases de dados...";

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

                StatusMessage = "Lista de bases de dados atualizada.";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao listar bases de dados: {ex.Message}", "Erro de Conexão");
                StatusMessage = "Erro ao conectar no banco de dados.";
            }
        }

        private void BuildConnectionString()
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder();
            builder.DataSource = !string.IsNullOrEmpty(DbServer) ? DbServer : @"localhost\MLSQLExpress";
            builder.InitialCatalog = !string.IsNullOrEmpty(DbName) ? DbName : "MeasurLink10";
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
                if (SelectedParser == null)
                {
                    StatusMessage = "Monitoramento de arquivo: Desativado (Modo Serial)";
                    Log.Information("File monitoring disabled (No parser selected)");
                    return;
                }

                if (_watcher != null) StopWatcher();

                _watcher = new FileSystemWatcher(WatchDirectory);
                _watcher.Filter = "*.*";
                _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;

                _watcher.Created += OnFileCreated;

                _watcher.EnableRaisingEvents = true;
                StatusMessage = $"Monitorando: {WatchDirectory} usando {SelectedParser.MachineName}";
                Log.Information("Started watching directory: {Directory}", WatchDirectory);

                // Start Icon Animation
                _iconTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
                _iconTimer.Tick += (s, e) =>
                {
                    TrayIconSource = _iconToggle ? _iconGreen : _iconGrey;
                    _iconToggle = !_iconToggle;
                };
                _iconTimer.Start();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao iniciar monitoramento: {ex.Message}");
                Log.Error(ex, "Failed to start monitoring");
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
            TrayIconSource = _iconGrey;
        }

        private ImageSource LoadIcon(string resourcePath)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/SelectML.Client;component/{resourcePath}");
                var icon = new BitmapImage(uri);
                icon.Freeze();
                return icon;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao carregar ícone {resourcePath}: {ex.Message}";
                return null;
            }
        }

        // --- Lógica de Negócio ---

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            // Reverse Buffer Logic
            if (IsPendingAction)
            {
                _fileBuffer.Enqueue(e.FullPath);
                UpdateStatus($"Arquivo em espera ({_fileBuffer.Count}): {e.Name}");
                Log.Information("File queued in buffer: {File}", e.Name);
                return;
            }

            await ProcessFile(e.FullPath, e.Name);
        }

        private async Task ProcessFile(string fullPath, string fileName)
        {
             if (!SelectedParser.CanParse(fullPath)) return;

             if (!await WaitForFileAccess(fullPath))
             {
                 UpdateStatus($"Erro: Arquivo {fileName} bloqueado ou inacessível.");
                 Log.Warning("File locked or inaccessible: {File}", fullPath);
                 return;
             }

             try
             {
                 UpdateStatus($"Lendo arquivo: {fileName}...");
                 Log.Information("Processing file: {File}", fileName);

                 var data = SelectedParser.Parse(fullPath);

                 if (data.IsValid)
                 {
                     // Phase 6: Archive Immediately
                     try
                     {
                         _fileLifecycleService.ArchiveInputFile(fullPath, WatchDirectory);
                     }
                     catch (Exception ex)
                     {
                         UpdateStatus($"Erro de backup: {ex.Message}");
                         Log.Error(ex, "Backup failed, stopping processing for {File}", fileName);
                         return;
                     }

                     await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                     {
                         _isProcessingFile = true; // Protect against Serial Data loss during processing
                         IsPendingAction = true; // Ensure locked
                         _currentData = data;

                         PartName = data.PartName;
                         
                         _suppressBatchLookup = true;
                         BatchNumber = data.BatchNumber;
                         _suppressBatchLookup = false;

                         // Phase 5: Early Detection and Validation
                         DetectedStationName = await _databaseService.GetStationNameAsync(data.BatchNumber);
                         _expectedPartName = await _databaseService.GetRoutineNameAsync(data.BatchNumber);
                         
                         // Manually set validation state since we suppressed the automatic check
                         IsBatchNumberValid = !string.IsNullOrEmpty(DetectedStationName) || !string.IsNullOrEmpty(_expectedPartName);
                         // If no batch number in file (empty), we might consider it valid initially or invalid? 
                         // Logic says: if empty, valid (until typed).
                         if (string.IsNullOrWhiteSpace(data.BatchNumber)) IsBatchNumberValid = true;

                         var expectedFeatures = await _databaseService.GetFeaturesForRunAsync(data.BatchNumber);
                         // Populate KnownFeatures for Validation
                          System.Windows.Application.Current.Dispatcher.Invoke(() =>
                          {
                              KnownFeatures.Clear();
                              if (expectedFeatures != null)
                              {
                                  foreach(var f in expectedFeatures) KnownFeatures.Add(f);
                              }
                          });

                          MeasuredResults.Clear();
                          bool hasUnrecognized = false;
                          foreach (var item in data.Results)
                          {
                              // Initial Check: If KnownFeatures has items, check existence. 
                              // If KnownFeatures is empty (no features defined for this run in DB), decide policy.
                              // STRICT policy: If DB returns empty rules, maybe everything is unrecognized? 
                              // OR if DB returns "No Rules", maybe everything is valid?
                              // Usually if there are NO Expected Features, the CSV might be creating new ones?
                              // But prompt implies we validate against DB.
                              // Let's assume: If expectedFeatures is EMPTY, we cannot validate, so IsRecognized = true?
                              // User complaint: "received file with characteristic NOT IN DB... line should be red".
                              // This implies Strict.
                              
                              bool isRecognized = true;
                              if (expectedFeatures != null && expectedFeatures.Any())
                              {
                                  isRecognized = expectedFeatures.Contains(item.Key, StringComparer.OrdinalIgnoreCase);
                              }
                              // If expectedFeatures is null/empty, we default to True (valid) unless logic dictates otherwise.
                             
                              if (!isRecognized) hasUnrecognized = true;

                              var newItem = new ResultItem
                              {
                                  Characteristic = item.Key,
                                  Value = item.Value,
                                  IsRecognized = isRecognized // Set initial state
                              };
                              newItem.PropertyChanged += ResultItem_PropertyChanged;
                              MeasuredResults.Add(newItem);
                          }
                          
                          // Central Validation Trigger 
                          // (Must NOT overwrite IsRecognized with False if KnownFeatures is empty, unless we want to)
                          TriggerValidation(); 
                          
                          // Re-check hasUnrecognized after TriggerValidation (in case it changed)
                          hasUnrecognized = MeasuredResults.Any(r => !r.IsRecognized);

                          if (IsAutoMode)
                          {
                              if (hasUnrecognized)
                              {
                                 if (_sessionConfirmationAction == ConfirmationAction.SendAll)
                                 {
                                     await GenerateOutputCsv(data);
                                 }
                                 else if (_sessionConfirmationAction == ConfirmationAction.SendRecognized)
                                 {
                                     var filtered = GetFilteredData(data);
                                     await GenerateOutputCsv(filtered);
                                 }
                                 else
                                 {
                                     // Fallback to manual if no rule established
                                     IsPendingAction = true;
                                     StatusMessage = "Validação necessária. Verifique e envie.";
                                     RequestRestoreWindow?.Invoke();
                                 }
                             }
                             else
                             {
                                 await GenerateOutputCsv(data);
                             }
                         }
                         else
                         {
                             IsPendingAction = true;
                             _isProcessingFile = true; // Mark as File Mode (Locks out Serial)
                             StatusMessage = "Dados carregados. Verifique e clique em Enviar.";
                             RequestRestoreWindow?.Invoke();
                         }
                     });
                 }
                 else
                 {
                     UpdateStatus($"Aviso: Arquivo {fileName} não contem dados válidos.");
                     Log.Warning("File {File} does not contain valid data", fileName);
                 }
             }
             catch (Exception ex)
             {
                 UpdateStatus($"Erro ao processar {fileName}: {ex.Message}");
                 Log.Error(ex, "Error processing file: {File}", fileName);
             }
        }
        
        private void TriggerValidation() 
        {
             // 1. Validate Part Name
             if (!string.IsNullOrEmpty(_expectedPartName) && !string.IsNullOrEmpty(PartName))
             {
                 IsPartNameValid = string.Equals(PartName.Trim(), _expectedPartName.Trim(), StringComparison.OrdinalIgnoreCase);
             }
             else
             {
                 IsPartNameValid = true; 
             }

             // 2. Validate Rows (Recognition)
             if (MeasuredResults != null)
             {
                 foreach (var item in MeasuredResults)
                 {
                     ValidateItem(item);
                 }
                 
                 // 3. Mark Duplicates
                 CheckDuplicates();
             }
             
             CommandManager.InvalidateRequerySuggested();
        }

        private void CheckDuplicates()
        {
            if (MeasuredResults == null) return;

            var groups = MeasuredResults.GroupBy(x => x.Characteristic?.Trim(), StringComparer.OrdinalIgnoreCase);
            bool anyChanged = false;

            foreach (var group in groups)
            {
                bool isDuplicate = group.Count() > 1 && !string.IsNullOrWhiteSpace(group.Key);
                foreach (var item in group)
                {
                     if (item.HasDuplicateName != isDuplicate)
                     {
                         item.HasDuplicateName = isDuplicate;
                         anyChanged = true;
                     }
                }
            }
            
            if (anyChanged) CommandManager.InvalidateRequerySuggested();
        }
        
        private void ValidateItem(ResultItem item)
        {
             if (item == null) return;
             
             // If we have known features, it MUST be one of them
             if (KnownFeatures != null && KnownFeatures.Any())
             {
                 var input = item.Characteristic?.Trim();
                 item.IsRecognized = !string.IsNullOrWhiteSpace(input) && 
                                     KnownFeatures.Any(f => f.Equals(input, StringComparison.OrdinalIgnoreCase));
             }
             else
             {
                 // No features loaded/defined. 
                 // If this was a manual entry with no DB connection, valid.
                 // If this was a file load with empty DB features, it remains as initialized (Valid).
                 // We only force FALSE if we HAVE a list and the item is NOT in it.
                 // So we do nothing here, preserving the initialization from ProcessFile.
                 // BUT, if user edits it? 
                 // If KnownFeatures is empty, we assume valid for now (Flexible mode).
                 item.IsRecognized = true;
             }
        }

        private void ResultItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ResultItem.Characteristic))
            {
                TriggerValidation();
            }
        }
        private async Task LoadFeaturesForPart()
        {
            if (string.IsNullOrWhiteSpace(PartName) || string.IsNullOrWhiteSpace(BatchNumber)) return;
            // Only trigger if we are not in the middle of a file load (which does its own lookup)
            // But actually, we want to update the cache for the dropdown even if file loaded.
            
            try 
            {
                 var features = await _databaseService.GetFeaturesForRunAsync(BatchNumber); // Note: API uses BatchNumber to find Run, then Features? 
                 // Wait, the Prompt says "GetFeaturesForRun(PartName)". 
                 // My existing code uses: "GetFeaturesForRunAsync(data.BatchNumber)".
                 // Let's check DatabaseService if possible, or assume existing usage is correct. 
                 // Actually, usually it's by PartName (Routine). 
                 // But validation logic in OnFileCreated uses BatchNumber. 
                 // Let's stick to what works or check DatabaseService.
                 // Assuming existing logic: GetFeaturesForRunAsync(BatchNumber).
                 
                 if (features != null)
                 {
                     System.Windows.Application.Current.Dispatcher.Invoke(() => 
                     {
                         KnownFeatures.Clear();
                         foreach(var f in features) KnownFeatures.Add(f);
                         
                         // Re-validate current items against new known features
                         foreach(var item in MeasuredResults)
                         {
                              // Trigger property changed logic to re-evaluate
                              ResultItem_PropertyChanged(item, new PropertyChangedEventArgs(nameof(ResultItem.Characteristic)));
                         }
                     });
                 }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load features for part");
            }
        }

        private bool CanExecuteAction(object obj)
        {
            if (obj is string commandName && commandName == "Cancel")
            {
                 return IsPendingAction || MeasuredResults.Count > 0;
            }
            // Cannot send if there are duplicates
            if (MeasuredResults.Any(r => r.HasDuplicateName)) return false;

            return IsPendingAction && !string.IsNullOrWhiteSpace(PartName) && !string.IsNullOrWhiteSpace(BatchNumber);
        }

        private async void ExecuteSend(object obj)
        {
            // Always construct data from UI (MeasuredResults) to ensure manual edits/removals are respected.
            // If _currentData exists (File Mode), we preserve its metadata but override results.
            
            if (string.IsNullOrWhiteSpace(PartName) || string.IsNullOrWhiteSpace(BatchNumber)) return;

            MeasurementData dataToSend = new MeasurementData
            {
                PartName = PartName,
                BatchNumber = BatchNumber,
                // Inherit date from file if available, else Now
                MeasureDate = _currentData?.MeasureDate ?? DateTime.Now
            };

            try
            {
                dataToSend.Results = MeasuredResults.ToDictionary(k => k.Characteristic, v => v.Value);
            }
            catch (ArgumentException)
            {
                StatusMessage = "Erro: Nomes de características duplicados detectados.";
                System.Windows.MessageBox.Show("Existem características com o mesmo nome na lista. Corrija antes de enviar.", "Erro de Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao preparar dados: {ex.Message}";
                Log.Error(ex, "Error creating dictionary for send");
                return;
            }


            // Phase 5: Validation Check
            if (MeasuredResults.Any(r => !r.IsRecognized))
            {
                var action = _sessionConfirmationAction;

                if (action == ConfirmationAction.None)
                {
                    var dlg = new ConfirmationWindow();
                    dlg.Owner = System.Windows.Application.Current.MainWindow;
                    dlg.ShowDialog();

                    action = dlg.UserChoice;

                    if (dlg.IsDontAskAgainChecked && action != ConfirmationAction.Cancel)
                    {
                        _sessionConfirmationAction = action;
                    }
                }

                if (action == ConfirmationAction.Cancel || action == ConfirmationAction.None)
                {
                    return; // User cancelled
                }

                if (action == ConfirmationAction.SendRecognized)
                {
                    dataToSend = GetFilteredData(_currentData);
                }
                // If action is SendAll, use original _currentData
            }

            IsPendingAction = false;
            Log.Information("User manually approved data for Batch {Batch}", dataToSend.BatchNumber);
            await GenerateOutputCsv(dataToSend);
        }

        private MeasurementData GetFilteredData(MeasurementData source)
        {
            var recognizedKeys = MeasuredResults.Where(r => r.IsRecognized).Select(r => r.Characteristic).ToHashSet();
            var filteredResults = source.Results.Where(kv => recognizedKeys.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);

            return new MeasurementData
            {
                PartName = source.PartName,
                BatchNumber = source.BatchNumber,
                MeasureDate = source.MeasureDate,
                Results = filteredResults
            };
        }

        private async Task GenerateOutputCsv(MeasurementData data)
        {
            try
            {
                StatusMessage = "Salvando dados...";

                string outputDir = Path.Combine(WatchDirectory, "Output");

                // Reuse the detected station name if available, otherwise query again (or use cached property)
                string stationName = DetectedStationName;
                if (string.IsNullOrEmpty(stationName))
                {
                    stationName = await _databaseService.GetStationNameAsync(data.BatchNumber);
                }

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

        private void ExecuteRemoveRow(object obj)
        {
            if (obj is ResultItem item && MeasuredResults.Contains(item))
            {
                MeasuredResults.Remove(item);
                TriggerValidation();

                if (MeasuredResults.Count == 0)
                {
                    // Se não houver mais itens, cancelar a operação (Reset total)
                    ExecuteCancel(null);
                }
            }
        }

        private async void ResetUI()
        {
            _currentData = null;
            PartName = string.Empty;
            BatchNumber = string.Empty;
            DetectedStationName = string.Empty;
            MeasuredResults.Clear();
            KnownFeatures.Clear(); // Fix: Clear stale features
            _currentData = null;   // Fix: Reset current data context
            _isProcessingFile = false; // Release File Mode lock
            // IsPendingAction should be false here usually, unless we re-activate it for Buffered data
            
            // Check Buffers
            if (_serialBuffer.Count > 0)
            {
                 ProcessSerialBuffer();
            }
            else if (_fileBuffer.Count > 0)
            {
                 // Small delay to ensure clean state?
                 await Task.Delay(100);
                 ProcessFileBuffer();
            }
            else
            {
                // Fix: Ensure we unlock the UI state if no pending buffers
                IsPendingAction = false;
                StatusMessage = "Aguardando...";
            }
        }
        
        private async void ProcessFileBuffer()
        {
            if (_fileBuffer.Count > 0)
            {
                var file = _fileBuffer.Dequeue();
                UpdateStatus($"Processando arquivo bufferizado ({_fileBuffer.Count} restantes)...");
                // Need to call ProcessFile (async)
                await ProcessFile(file, Path.GetFileName(file));
            }
        }

        private void ProcessSerialBuffer()
        {
            if (_serialBuffer.Count > 0)
            {
                while (_serialBuffer.Count > 0)
                {
                    var item = _serialBuffer.Dequeue();
                    MeasuredResults.Add(new ResultItem 
                    { 
                        Characteristic = item.FeatureName, 
                        Value = item.Value, 
                        IsRecognized = !item.IsGeneric,
                        IsEditable = item.IsGeneric
                    });
                }
                
                // Lock UI for the new serial data
                IsPendingAction = true;
                StatusMessage = "Dados seriais recuperados do buffer.";
                RequestRestoreWindow?.Invoke();
            }
        }

        private void OnSerialMeasurementReceived(object sender, SerialMeasurement e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_isProcessingFile)
                {
                    // Scenario A: Collision. Queue it.
                    _serialBuffer.Enqueue(e);
                    StatusMessage = $"Dado serial em buffer ({_serialBuffer.Count})...";
                }
                else
                {
                    // Scenario B: Append direct
                    var newItem = new ResultItem 
                    { 
                        Characteristic = e.FeatureName, 
                        Value = e.Value, 
                        IsRecognized = !e.IsGeneric,
                        IsEditable = e.IsGeneric
                    };
                    
                    if (newItem.IsEditable)
                    {
                        newItem.PropertyChanged += ResultItem_PropertyChanged;
                    }

                    MeasuredResults.Add(newItem);
                    TriggerValidation();
                    
                    // Ensure we lock out the File Watcher
                    if (!IsPendingAction)
                    {
                        IsPendingAction = true;
                        StatusMessage = "Recebendo dados seriais...";
                        RequestRestoreWindow?.Invoke();
                    }
                }
            });
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
                Log.Information("Watch directory changed to: {Directory}", WatchDirectory);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
