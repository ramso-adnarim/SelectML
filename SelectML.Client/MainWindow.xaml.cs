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

            // Subscribe to VM PropertyChanged to update the tray icon and window icon
            vm.PropertyChanged += (s, ev) =>
            {
                if (ev.PropertyName == nameof(vm.TrayIconSource))
                {
                    UpdateNotifyIcon(vm.TrayIconSource);
                }
                else if (ev.PropertyName == nameof(vm.IsDarkMode))
                {
                    UpdateWindowIcon(vm.IsDarkMode);
                }
            };

            // Set initial icons
            UpdateNotifyIcon(vm.TrayIconSource);
            UpdateWindowIcon(vm.IsDarkMode);

            // Subscribe to VM events
            vm.RequestShowBalloonTip += (title, msg, iconType) =>
            {
                var icon = BalloonIcon.Info;
                if (iconType == "Warning") icon = BalloonIcon.Warning;
                else if (iconType == "Error") icon = BalloonIcon.Error;
                MyNotifyIcon.ShowBalloonTip(title, msg, icon);
            };
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

            // Click outside to deselect
            this.MouseDown += (s, e) =>
            {
                // Check if the click is outside the DataGrid
                // We do a simple check: if the source is the Window or a container that isn't the DataGrid
                // Logic: logic to clear selection if original source is not part of DataGrid
                if (e.OriginalSource is DependencyObject obj && !IsDescendantOf(obj, ResultsDataGrid))
                {
                   ResultsDataGrid.SelectedItem = null;
                   // Clear focus to ensure visual state updates if needed
                   Keyboard.ClearFocus();
                }
            };
        }
        
        private bool IsDescendantOf(DependencyObject node, DependencyObject ancestor)
        {
            if (ancestor == null) return false;
            while (node != null)
            {
                if (node == ancestor) return true;
                node = VisualTreeHelper.GetParent(node);
            }
            return false;
        }


        private void DbPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel vm)
            {
                vm.DbPassword = DbPasswordBox.Password;
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
            if (_currentIcon != null)
            {
                DestroyIcon(_currentIcon.Handle);
            }

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

        private System.Drawing.Icon? _currentIcon;

        private void UpdateNotifyIcon(ImageSource imageSource)
        {
            if (imageSource is BitmapSource bitmapSource)
            {
                try
                {
                    using (var outStream = new System.IO.MemoryStream())
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(outStream);
                        outStream.Position = 0;
                        using (var bitmap = new System.Drawing.Bitmap(outStream))
                        {
                            IntPtr hIcon = bitmap.GetHicon();
                            var icon = System.Drawing.Icon.FromHandle(hIcon);

                            var oldIcon = _currentIcon;
                            MyNotifyIcon.Icon = icon;
                            _currentIcon = icon;

                            if (oldIcon != null)
                            {
                                DestroyIcon(oldIcon.Handle);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error converting tray icon: {ex.Message}");
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private void UpdateWindowIcon(bool isDarkMode)
        {
            try
            {
                string iconPath = isDarkMode ? "Resources/SelectML-logo-short-dark.ico" : "Resources/SelectML-logo-short-light.ico";
                var uri = new Uri($"pack://application:,,,/SelectML.Client;component/{iconPath}");
                var icon = new BitmapImage(uri);
                icon.Freeze();
                this.Icon = icon;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating window icon: {ex.Message}");
            }
        }
    }
}
