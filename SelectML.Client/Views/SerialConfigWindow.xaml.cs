using System.Windows;

namespace SelectML.Client.Views
{
    public partial class SerialConfigWindow : Window
    {
        public SerialConfigWindow()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
