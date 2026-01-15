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
            // Update ResourceDictionary
            var dict = new ResourceDictionary();
            string themeName = isDark ? "Dark" : "Light";
            dict.Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative);

            // Clear old theme and add new one
            this.Resources.MergedDictionaries.Clear();
            this.Resources.MergedDictionaries.Add(dict);

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
