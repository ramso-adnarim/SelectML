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
using WpfTextBox = System.Windows.Controls.TextBox;

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
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }
                Topmost = true;
                Activate();
                Focus();
                Topmost = false;
            };
            vm.RequestMinimizeWindow += () =>
            {
                WindowState = WindowState.Minimized;
            };
            vm.RequestMinimizeToTray += () =>
            {
                WindowState = WindowState.Minimized;
                Hide();
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
                var configService = new SelectML.Client.Services.ConfigService();
                var config = configService.Load();

                if (!double.IsNaN(config.WindowTop) && !double.IsNaN(config.WindowLeft) &&
                    config.WindowTop >= SystemParameters.VirtualScreenTop &&
                    config.WindowLeft >= SystemParameters.VirtualScreenLeft &&
                    config.WindowTop + 50 <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight &&
                    config.WindowLeft + 50 <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth)
                {
                    this.Top = config.WindowTop;
                    this.Left = config.WindowLeft;
                }

                if (config.WindowHeight > 200) this.Height = config.WindowHeight;
                if (config.WindowWidth > 300) this.Width = config.WindowWidth;

                if (config.WindowState == (int)WindowState.Maximized)
                {
                    this.WindowState = WindowState.Maximized;
                }
                else
                {
                    this.WindowState = WindowState.Normal;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring window placement: {ex.Message}");
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
                var configService = new SelectML.Client.Services.ConfigService();
                var config = configService.Load();

                if (this.WindowState == WindowState.Normal)
                {
                    config.WindowTop = this.Top;
                    config.WindowLeft = this.Left;
                    config.WindowHeight = this.Height;
                    config.WindowWidth = this.Width;
                }
                else if (this.RestoreBounds != Rect.Empty)
                {
                    config.WindowTop = this.RestoreBounds.Top;
                    config.WindowLeft = this.RestoreBounds.Left;
                    config.WindowHeight = this.RestoreBounds.Height;
                    config.WindowWidth = this.RestoreBounds.Width;
                }

                config.WindowState = (int)this.WindowState;
                configService.Save(config);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving window placement: {ex.Message}");
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
                string iconName = isDarkMode ? "SelectML-logo-short-dark.ico" : "SelectML-logo-short-light.ico";
                var uri = new Uri($"pack://application:,,,/Resources/{iconName}");
                this.Icon = BitmapFrame.Create(uri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating window icon: {ex.Message}");
            }
        }

        private void ValBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (sender is WpfTextBox currentTextBox)
                {
                    // Força a atualização do binding do valor digitado
                    var binding = currentTextBox.GetBindingExpression(WpfTextBox.TextProperty);
                    binding?.UpdateSource();

                    // Identifica a linha atual
                    var row = FindVisualParent<DataGridRow>(currentTextBox);
                    if (row != null)
                    {
                        int currentIndex = ResultsDataGrid.ItemContainerGenerator.IndexFromContainer(row);
                        int targetIndex = (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) ? currentIndex - 1 : currentIndex + 1;

                        if (targetIndex >= 0 && targetIndex < ResultsDataGrid.Items.Count)
                        {
                            NavigateAndFocusValueInput(targetIndex);
                        }
                    }

                    e.Handled = true;
                }
            }
        }

        private void ResultsDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                int currentIndex = ResultsDataGrid.SelectedIndex;
                if (currentIndex >= 0)
                {
                    int targetIndex = (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) ? currentIndex - 1 : currentIndex + 1;
                    if (targetIndex >= 0 && targetIndex < ResultsDataGrid.Items.Count)
                    {
                        NavigateAndFocusValueInput(targetIndex);
                        e.Handled = true;
                    }
                }
            }
        }

        private void NavigateAndFocusValueInput(int targetIndex)
        {
            ResultsDataGrid.SelectedIndex = targetIndex;
            ResultsDataGrid.ScrollIntoView(ResultsDataGrid.Items[targetIndex]);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var targetRow = (DataGridRow)ResultsDataGrid.ItemContainerGenerator.ContainerFromIndex(targetIndex);
                if (targetRow != null)
                {
                    // Foca o TextBox da coluna Valor
                    var textBox = FindVisualChildByName<WpfTextBox>(targetRow, "ValBox");
                    if (textBox != null && textBox.IsEnabled)
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                        return;
                    }

                    // Se for linha de atributo, foca o ToggleButton
                    var toggle = FindVisualChild<System.Windows.Controls.Primitives.ToggleButton>(targetRow);
                    if (toggle != null && toggle.IsEnabled)
                    {
                        toggle.Focus();
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void ResultsDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var depObj = e.OriginalSource as DependencyObject;
            var row = FindVisualParent<DataGridRow>(depObj);
            if (row != null)
            {
                row.IsSelected = true;
                if (row.ContextMenu != null)
                {
                    row.ContextMenu.PlacementTarget = row;
                    row.ContextMenu.IsOpen = true;
                    e.Handled = true; // Suprime o menu de contexto nativo do Windows no TextBox
                }
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                {
                    return parent;
                }
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private static T? FindVisualChildByName<T>(DependencyObject? parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && typedChild.Name == name)
                {
                    return typedChild;
                }

                var descendant = FindVisualChildByName<T>(child, name);
                if (descendant != null)
                {
                    return descendant;
                }
            }
            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }
            return null;
        }

        private void OnTraceabilityPopupPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.IsTraceabilityMenuOpen = false;
                }
                e.Handled = true;
            }
        }

        private void OnTraceabilityTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Contains(';') || e.Text.Contains('\r') || e.Text.Contains('\n'))
            {
                e.Handled = true;
            }
        }

        private void OnTraceabilityPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText, true))
            {
                string text = (string)e.DataObject.GetData(System.Windows.DataFormats.UnicodeText);
                if (text != null && (text.Contains(';') || text.Contains('\r') || text.Contains('\n')))
                {
                    e.CancelCommand();
                }
            }
        }
    }
}
