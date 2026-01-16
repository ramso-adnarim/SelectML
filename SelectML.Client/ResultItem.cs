using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SelectML.Client
{
    public class ResultItem : INotifyPropertyChanged
    {
        private string _characteristic;
        private double _value;
        private bool _isRecognized = true;
        private bool _isEditable = false;

        public string Characteristic
        {
            get => _characteristic;
            set { _characteristic = value; OnPropertyChanged(); }
        }

        public double Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public bool IsRecognized
        {
            get => _isRecognized;
            set { _isRecognized = value; OnPropertyChanged(); }
        }

        public bool IsEditable
        {
            get => _isEditable;
            set { _isEditable = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
