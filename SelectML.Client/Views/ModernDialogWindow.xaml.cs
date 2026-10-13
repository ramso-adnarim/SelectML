using System;
using System.Windows;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;

namespace SelectML.Client.Views
{
    public enum ModernDialogButtons
    {
        OK,
        YesNo
    }

    public enum ModernDialogIcon
    {
        Information,
        Warning,
        Error,
        Question
    }

    public partial class ModernDialogWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public ModernDialogWindow()
        {
            InitializeComponent();
        }

        public static MessageBoxResult Show(string message, string title = "SelectML", ModernDialogButtons buttons = ModernDialogButtons.OK, ModernDialogIcon icon = ModernDialogIcon.Information, Window? owner = null)
        {
            var win = new ModernDialogWindow();
            var targetOwner = owner ?? WpfApplication.Current?.MainWindow;
            if (targetOwner != null && targetOwner.IsVisible)
            {
                win.Owner = targetOwner;
            }
            win.Title = title;
            win.DialogTitleText.Text = title;
            win.DialogMessageText.Text = message;

            // Configure Buttons
            if (buttons == ModernDialogButtons.YesNo)
            {
                win.BtnOk.Visibility = Visibility.Collapsed;
                win.BtnYes.Visibility = Visibility.Visible;
                win.BtnNo.Visibility = Visibility.Visible;
                win.BtnYes.IsDefault = true;
            }
            else
            {
                win.BtnOk.Visibility = Visibility.Visible;
                win.BtnYes.Visibility = Visibility.Collapsed;
                win.BtnNo.Visibility = Visibility.Collapsed;
                win.BtnOk.IsDefault = true;
            }

            // Configure Icon
            switch (icon)
            {
                case ModernDialogIcon.Warning:
                    win.IconGlyph.Text = "⚠";
                    win.IconContainer.Background = (win.TryFindResource("Brush.Status.Warning") as WpfBrush) ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
                    break;
                case ModernDialogIcon.Error:
                    win.IconGlyph.Text = "✕";
                    win.IconContainer.Background = (win.TryFindResource("Brush.Status.Error") as WpfBrush) ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                    break;
                case ModernDialogIcon.Question:
                    win.IconGlyph.Text = "?";
                    win.IconContainer.Background = (win.TryFindResource("Brush.Action.Primary") as WpfBrush) ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246));
                    break;
                case ModernDialogIcon.Information:
                default:
                    win.IconGlyph.Text = "ℹ";
                    win.IconContainer.Background = (win.TryFindResource("Brush.Action.Primary") as WpfBrush) ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246));
                    break;
            }

            win.ShowDialog();
            return win.Result;
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            DialogResult = true;
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            DialogResult = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            DialogResult = true;
        }
    }
}
