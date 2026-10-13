using System.Windows;
using SelectML.Client.ViewModels;
using SelectML.Core;

namespace SelectML.Client.Views
{
    public partial class TemplateConfigWindow : Window
    {
        public TemplateConfigViewModel ViewModel { get; }

        public TemplateConfigWindow(IConfigurableParser? parser = null)
        {
            InitializeComponent();
            ViewModel = new TemplateConfigViewModel(parser);
            DataContext = ViewModel;

            ViewModel.RequestClose += (result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
