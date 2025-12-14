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
            this.DataContext = new MainViewModel();

            // Load window placement from settings
            LoadWindowPlacement();

            // Initialize PasswordBox if ViewModel has value
            if (this.DataContext is MainViewModel vm && !string.IsNullOrEmpty(vm.DbPassword))
            {
               // Note: Accessing DbPasswordBox requires it to be named in XAML
               // Since we are in Constructor, standard FindName might not work yet if template is not applied,
               // but for Window content it should be fine after InitializeComponent.
               // However, to keep it simple and robust, we rely on the user re-entering password if needed
               // OR we can't easily set it back without exposing it.
               // For this task, setting the password box from code behind is acceptable.
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

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
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
