using System.Windows;

namespace SelectML.Client.Views
{
    public partial class ConfirmationWindow : Window
    {
        public bool IsDontAskAgainChecked => DontAskCheckBox.IsChecked ?? false;
        public ConfirmationAction UserChoice { get; private set; } = ConfirmationAction.None;

        public ConfirmationWindow()
        {
            InitializeComponent();
        }

        private void SendAll_Click(object sender, RoutedEventArgs e)
        {
            UserChoice = ConfirmationAction.SendAll;
            DialogResult = true;
            Close();
        }

        private void SendRecognized_Click(object sender, RoutedEventArgs e)
        {
            UserChoice = ConfirmationAction.SendRecognized;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            UserChoice = ConfirmationAction.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
