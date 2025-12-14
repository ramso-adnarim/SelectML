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
    }
}