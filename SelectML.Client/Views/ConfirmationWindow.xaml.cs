using System.Windows;

namespace SelectML.Client.Views
{
    public partial class ConfirmationWindow : Window
    {
        public bool IsDontAskAgainChecked => DontAskCheckBox.IsChecked ?? false;

        public ConfirmationWindow()
        {
            InitializeComponent();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
