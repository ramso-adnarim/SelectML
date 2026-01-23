using System;
using System.Configuration;
using System.Data;
using System.Windows;
using Serilog;
using System.Windows.Media.Imaging;
using Velopack;

namespace SelectML.Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Initialize Velopack
            VelopackApp.Build()
                .Run();

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("Application Starting Up");

            base.OnStartup(e);

            // Instantiate MainWindow manually to ensure theme is applied before Show
            var mainWindow = new MainWindow();
            this.MainWindow = mainWindow;

            // Restore theme from settings
            bool isDark = SelectML.Client.Properties.Settings.Default.IsDarkMode;
            SetTheme(isDark);

            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application Exiting");
            Log.CloseAndFlush();
            base.OnExit(e);
        }

        public void SetTheme(bool isDark)
        {
            string themeName = isDark ? "Dark" : "Light";

            // Clear old theme and add new one
            this.Resources.MergedDictionaries.Clear();
            
            // 1. Theme (Colors)
            var themeDict = new ResourceDictionary();
            themeDict.Source = new Uri($"pack://application:,,,/SelectML.Client;component/Themes/{themeName}.xaml");
            this.Resources.MergedDictionaries.Add(themeDict);

            // 2. Control Styles
            var controlsDict = new ResourceDictionary();
            controlsDict.Source = new Uri("pack://application:,,,/SelectML.Client;component/Styles/Controls.xaml");
            this.Resources.MergedDictionaries.Add(controlsDict);

            // 3. DataGrid Styles
            var dataGridDict = new ResourceDictionary();
            dataGridDict.Source = new Uri("pack://application:,,,/SelectML.Client;component/Styles/DataGrid.xaml");
            this.Resources.MergedDictionaries.Add(dataGridDict);

            // Update Window Icon
            if (this.MainWindow != null)
            {
                 string iconName = isDark ? "SelectML-logo-short-dark.ico" : "SelectML-logo-short-light.ico";
                 try
                 {
                    var iconUri = new Uri($"pack://application:,,,/Resources/{iconName}");
                    this.MainWindow.Icon = BitmapFrame.Create(iconUri);
                 }
                 catch (Exception ex)
                 {
                     Log.Error(ex, "Failed to set window icon");
                 }
            }
        }
    }
}
