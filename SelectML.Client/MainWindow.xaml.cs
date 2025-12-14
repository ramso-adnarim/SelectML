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
        }
    }
}