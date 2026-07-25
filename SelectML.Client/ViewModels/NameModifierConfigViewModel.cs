using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Text.RegularExpressions;
using SelectML.Client.MVVM;
using SelectML.Client.Services;

namespace SelectML.Client.ViewModels
{
    public class NameModifierConfigViewModel : INotifyPropertyChanged
    {
        private readonly ConfigService _configService;
        private string _customNameModifierFormat;
        private int _decimals;
        private string _roundingMode;
        private string _previewText;

        public event PropertyChangedAction RequestInsertToken;
        public event Action RequestClose;
        public event PropertyChangedEventHandler PropertyChanged;

        public NameModifierConfigViewModel()
        {
            _configService = new ConfigService();
            LoadConfiguration();

            InsertTokenCommand = new RelayCommand(ExecuteInsertToken);
            SaveCommand = new RelayCommand(ExecuteSave);
            CancelCommand = new RelayCommand(ExecuteCancel);

            UpdatePreview();
        }

        public string CustomNameModifierFormat
        {
            get => _customNameModifierFormat;
            set
            {
                _customNameModifierFormat = value;
                OnPropertyChanged(nameof(CustomNameModifierFormat));
                OnPropertyChanged(nameof(CanInsertNominal));
                OnPropertyChanged(nameof(CanInsertTolerance));
                OnPropertyChanged(nameof(CanConfigureTags));
                OnPropertyChanged(nameof(HasBrokenTags));
                OnPropertyChanged(nameof(CanSave));
                UpdatePreview();
            }
        }

        public int Decimals
        {
            get => _decimals;
            set
            {
                if (value >= 0 && value <= 9)
                {
                    _decimals = value;
                }
                OnPropertyChanged(nameof(Decimals)); // Always notify to revert invalid inputs
            }
        }

        public bool IsRoundingMode
        {
            get => _roundingMode == "Round";
            set
            {
                if (value)
                {
                    _roundingMode = "Round";
                    OnPropertyChanged(nameof(IsRoundingMode));
                    OnPropertyChanged(nameof(IsTruncateMode));
                }
            }
        }

        public bool IsTruncateMode
        {
            get => _roundingMode == "Truncate";
            set
            {
                if (value)
                {
                    _roundingMode = "Truncate";
                    OnPropertyChanged(nameof(IsRoundingMode));
                    OnPropertyChanged(nameof(IsTruncateMode));
                }
            }
        }

        public string PreviewText
        {
            get => _previewText;
            set
            {
                _previewText = value;
                OnPropertyChanged(nameof(PreviewText));
            }
        }

        public bool CanInsertNominal => string.IsNullOrEmpty(_customNameModifierFormat) || !_customNameModifierFormat.Contains("{N,");
        
        public bool CanInsertTolerance => string.IsNullOrEmpty(_customNameModifierFormat) || !_customNameModifierFormat.Contains("{T,");

        public bool CanConfigureTags => CanInsertNominal || CanInsertTolerance;

        public bool HasBrokenTags
        {
            get
            {
                if (string.IsNullOrEmpty(_customNameModifierFormat)) return false;
                string noValidTags = Regex.Replace(_customNameModifierFormat, @"\{[NT],\d,[AT]\}", "");
                return noValidTags.Contains("{") || noValidTags.Contains("}");
            }
        }

        public bool CanSave => !HasBrokenTags;

        public ICommand InsertTokenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        private void LoadConfiguration()
        {
            var config = _configService.Load();
            _customNameModifierFormat = config.CustomNameModifierFormat ?? "{N,2,A} {T,3,A}";
            _decimals = 2; // Default for UI
            _roundingMode = "Round";
        }

        private void ExecuteInsertToken(object obj)
        {
            if (obj is string tokenType)
            {
                string modeChar = _roundingMode == "Round" ? "A" : "T";
                string token = "";
                
                if (tokenType == "Nominal")
                    token = $"{{N,{_decimals},{modeChar}}}";
                else if (tokenType == "Tolerancia")
                    token = $"{{T,{_decimals},{modeChar}}}";
                
                if (!string.IsNullOrEmpty(token))
                {
                    RequestInsertToken?.Invoke(token);
                }
            }
        }

        private void ExecuteSave(object obj)
        {
            if (!CanSave) return;
            var config = _configService.Load();
            config.CustomNameModifierFormat = CustomNameModifierFormat;
            _configService.Save(config);

            RequestClose?.Invoke();
        }

        private void ExecuteCancel(object obj)
        {
            RequestClose?.Invoke();
        }

        public void UpdatePreview()
        {
            double nom = 2.5555;
            double sup = 0.0555;
            double inf = 0.0555;
            string symbol = "Ø";

            string format = CustomNameModifierFormat ?? "";

            // Process Nominal {N,decimals,mode}
            format = Regex.Replace(format, @"\{N,(\d+),([AT])\}", match =>
            {
                int dec = int.Parse(match.Groups[1].Value);
                string mode = match.Groups[2].Value == "T" ? "Truncate" : "Round";
                string val = ApplyDecimals(nom, dec, mode);
                return $"{symbol}{val}"; // Auto-prepend symbol
            });

            // Process Tolerance {T,decimals,mode}
            format = Regex.Replace(format, @"\{T,(\d+),([AT])\}", match =>
            {
                int dec = int.Parse(match.Groups[1].Value);
                string mode = match.Groups[2].Value == "T" ? "Truncate" : "Round";
                // Preview mock: sup == inf -> ±sup
                return $"±{ApplyDecimals(sup, dec, mode)}";
            });

            PreviewText = format;
        }

        private string ApplyDecimals(double value, int decimals, string mode)
        {
            if (mode == "Truncate")
            {
                double multiplier = Math.Pow(10, decimals);
                double truncated = Math.Truncate(value * multiplier) / multiplier;
                return truncated.ToString($"F{decimals}", new System.Globalization.CultureInfo("pt-BR"));
            }
            else
            {
                return Math.Round(value, decimals).ToString($"F{decimals}", new System.Globalization.CultureInfo("pt-BR"));
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        public delegate void PropertyChangedAction(string text);
    }
}
