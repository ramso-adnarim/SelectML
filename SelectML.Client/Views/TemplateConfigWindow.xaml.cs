using System.Windows;
using SelectML.Core;

namespace SelectML.Client.Views
{
    public partial class TemplateConfigWindow : Window
    {
        private readonly IConfigurableParser? _parser;

        public TemplateConfigWindow(IConfigurableParser? parser = null)
        {
            InitializeComponent();
            _parser = parser;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
