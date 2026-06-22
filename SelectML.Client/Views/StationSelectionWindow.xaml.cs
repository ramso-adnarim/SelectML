using System.Collections.Generic;
using System.Windows;

namespace SelectML.Client.Views
{
    public enum StationSelectionChoice
    {
        Cancel,
        SendUnidentified,
        SendWithStation
    }

    public partial class StationSelectionWindow : Window
    {
        public StationSelectionChoice UserChoice { get; private set; } = StationSelectionChoice.Cancel;
        public string SelectedStation { get; private set; }

        public StationSelectionWindow(List<string> stations)
        {
            InitializeComponent();
            
            CmbStations.ItemsSource = stations;
            if (stations.Count > 0)
            {
                CmbStations.SelectedIndex = 0;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            UserChoice = StationSelectionChoice.Cancel;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            UserChoice = StationSelectionChoice.SendUnidentified;
            Close();
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (CmbStations.SelectedItem != null)
            {
                SelectedStation = CmbStations.SelectedItem.ToString();
                UserChoice = StationSelectionChoice.SendWithStation;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Selecione uma estação primeiro.", "Aviso", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
    }
}
