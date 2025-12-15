using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Hardcodet.Wpf.TaskbarNotification;
using SelectML.Client.ViewModels;

namespace SelectML.Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Aqui vinculamos o ViewModel à Tela
            // Isso garante que o código só rode quando a aplicação iniciar,
            // evitando erros no editor visual do Visual Studio.
            var vm = new MainViewModel();
            this.DataContext = vm;

            // Subscribe to VM events
            vm.RequestShowBalloonTip += (title, msg) => MyNotifyIcon.ShowBalloonTip(title, msg, BalloonIcon.Info);
            vm.RequestRestoreWindow += () =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };
            vm.RequestMinimizeWindow += () =>
            {
                WindowState = WindowState.Minimized;
            };

            // Load window placement from settings
            LoadWindowPlacement();

            // Initialize PasswordBox if ViewModel has value
            if (!string.IsNullOrEmpty(vm.DbPassword))
            {
               DbPasswordBox.Password = vm.DbPassword;
            }
        }

        private void DbPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel vm)
            {
                vm.DbPassword = ((PasswordBox)sender).Password;
            }
        }

        private void LoadWindowPlacement()
        {
            try
            {
                var settings = Properties.Settings.Default;

                // Only restore if valid values exist (e.g. not defaults if default is 0 or -1, but here defaults are set in .settings)
                // We check if values are within virtual screen bounds to avoid off-screen window
                if (settings.WindowTop >= SystemParameters.VirtualScreenTop &&
                    settings.WindowLeft >= SystemParameters.VirtualScreenLeft)
                {
                    this.Top = settings.WindowTop;
                    this.Left = settings.WindowLeft;
                    this.Height = settings.WindowHeight;
                    this.Width = settings.WindowWidth;
                    this.WindowState = settings.WindowState;
                }
            }
            catch
            {
                // Fallback to default center screen (configured in XAML usually, or let OS decide)
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            MyNotifyIcon.Dispose();

            try
            {
                var settings = Properties.Settings.Default;

                if (this.WindowState == WindowState.Normal)
                {
                    settings.WindowTop = this.Top;
                    settings.WindowLeft = this.Left;
                    settings.WindowHeight = this.Height;
                    settings.WindowWidth = this.Width;
                }
                else
                {
                    settings.WindowTop = this.RestoreBounds.Top;
                    settings.WindowLeft = this.RestoreBounds.Left;
                    settings.WindowHeight = this.RestoreBounds.Height;
                    settings.WindowWidth = this.RestoreBounds.Width;
                }

                settings.WindowState = this.WindowState;
                settings.Save();
            }
            catch
            {
                // Ignore errors during save on exit
            }

            base.OnClosing(e);
        }
    }
}
