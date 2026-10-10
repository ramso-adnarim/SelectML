using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SelectML.Client
{
    public enum ToleranceStatus
    {
        Pending,        // Aguardando medição (neutro)
        Conforming,     // Dentro da tolerância ou Aprovado (verde)
        OutOfTolerance, // Fora da tolerância ou Reprovado (vermelho)
        NotEvaluated    // Sem tolerâncias cadastradas (neutro)
    }

    public class ResultItem : INotifyPropertyChanged
    {
        private string _originalCharacteristic = string.Empty;
        private string _characteristic = string.Empty;
        private double? _value;
        private bool _isRecognized = true;
        private bool _isEditable = false;
        private bool _hasDuplicateName;
        private bool _isFromSerial;

        // Routine & Database Information
        private int _orderNo;
        private int _featureID;
        private short _featureType = 1; // 1 = Variável, 2/4 = Atributo, 3 = Posição
        private string _tipoDescricao = "Variável";
        private bool _isApproved = true; // Para atributos: true = Aprovado (0), false = Reprovado (1)
        private double? _nominal;
        private double? _lsl;
        private double? _usl;
        private int? _decimalLength;
        private bool _isFromDbRoutine = false;

        public string OriginalCharacteristic
        {
            get => _originalCharacteristic ?? _characteristic;
            set { _originalCharacteristic = value; OnPropertyChanged(); }
        }

        public string Characteristic
        {
            get => _characteristic;
            set { _characteristic = value; OnPropertyChanged(); }
        }

        public double? Value
        {
            get => _value;
            set 
            { 
                _value = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(HasValue));
                OnPropertyChanged(nameof(DisplayValue));
                OnPropertyChanged(nameof(Status));
            }
        }

        public bool HasValue => _value.HasValue;

        public int OrderNo
        {
            get => _orderNo;
            set { _orderNo = value; OnPropertyChanged(); }
        }

        public int FeatureID
        {
            get => _featureID;
            set { _featureID = value; OnPropertyChanged(); }
        }

        public short FeatureType
        {
            get => _featureType;
            set 
            { 
                _featureType = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsAttribute));
                OnPropertyChanged(nameof(Status));
            }
        }

        public string TipoDescricao
        {
            get => _tipoDescricao;
            set { _tipoDescricao = value; OnPropertyChanged(); }
        }

        public bool IsAttribute => _featureType == 2 || _featureType == 4;

        public bool IsApproved
        {
            get => _isApproved;
            set 
            { 
                _isApproved = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(Status));
            }
        }

        public double? Nominal
        {
            get => _nominal;
            set { _nominal = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
        }

        public double? LSL
        {
            get => _lsl;
            set { _lsl = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
        }

        public double? USL
        {
            get => _usl;
            set { _usl = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
        }

        public int? DecimalLength
        {
            get => _decimalLength;
            set { _decimalLength = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayValue)); }
        }

        public bool IsFromDbRoutine
        {
            get => _isFromDbRoutine;
            set { _isFromDbRoutine = value; OnPropertyChanged(); }
        }

        public ToleranceStatus Status
        {
            get
            {
                if (IsAttribute)
                {
                    return IsApproved ? ToleranceStatus.Conforming : ToleranceStatus.OutOfTolerance;
                }

                if (!_value.HasValue)
                {
                    return ToleranceStatus.Pending;
                }

                double val = _value.Value;

                if (LSL.HasValue && USL.HasValue)
                {
                    return (val >= LSL.Value && val <= USL.Value)
                        ? ToleranceStatus.Conforming
                        : ToleranceStatus.OutOfTolerance;
                }

                if (USL.HasValue && !LSL.HasValue)
                {
                    return (val <= USL.Value)
                        ? ToleranceStatus.Conforming
                        : ToleranceStatus.OutOfTolerance;
                }

                if (LSL.HasValue && !USL.HasValue)
                {
                    return (val >= LSL.Value)
                        ? ToleranceStatus.Conforming
                        : ToleranceStatus.OutOfTolerance;
                }

                return ToleranceStatus.NotEvaluated;
            }
        }

        public string DisplayValue
        {
            get
            {
                if (!_value.HasValue) return string.Empty;
                if (DecimalLength.HasValue && DecimalLength.Value >= 0)
                {
                    return _value.Value.ToString($"F{DecimalLength.Value}", CultureInfo.InvariantCulture);
                }
                return _value.Value.ToString(CultureInfo.InvariantCulture);
            }
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

        public bool HasDuplicateName
        {
            get => _hasDuplicateName;
            set { _hasDuplicateName = value; OnPropertyChanged(); }
        }

        public bool IsFromSerial
        {
            get => _isFromSerial;
            set { _isFromSerial = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
