using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SelectML.Core
{
    /// <summary>
    /// Representa um par de dados de rastreabilidade (Nome e Valor)
    /// cadastrado pelo usuário para injeção no CSV final enviado ao MeasurLink.
    /// </summary>
    public class TraceabilityItem : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _value = string.Empty;

        public TraceabilityItem()
        {
        }

        public TraceabilityItem(string key, string value)
        {
            _key = key ?? string.Empty;
            _value = value ?? string.Empty;
        }

        /// <summary>
        /// Nome / Identificador da rastreabilidade (ex: "Série", "Operador", "Turno").
        /// </summary>
        public string Key
        {
            get => _key;
            set
            {
                if (_key != value)
                {
                    _key = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsComplete));
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(IsKeyInvalid));
                    OnPropertyChanged(nameof(IsValueInvalid));
                }
            }
        }

        /// <summary>
        /// Conteúdo / Valor da rastreabilidade (ex: "Metrologia", "João", "Manhã").
        /// </summary>
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsComplete));
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(IsKeyInvalid));
                    OnPropertyChanged(nameof(IsValueInvalid));
                }
            }
        }

        /// <summary>
        /// Indica se ambos os campos (Nome E Valor) estão preenchidos de forma válida.
        /// </summary>
        [JsonIgnore]
        public bool IsComplete => !string.IsNullOrWhiteSpace(_key) && !string.IsNullOrWhiteSpace(_value);

        /// <summary>
        /// Indica se ambos os campos estão em branco.
        /// </summary>
        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrWhiteSpace(_key) && string.IsNullOrWhiteSpace(_value);

        /// <summary>
        /// Destaque de validação visual: Nome está vazio, mas o Valor foi preenchido.
        /// </summary>
        [JsonIgnore]
        public bool IsKeyInvalid => string.IsNullOrWhiteSpace(_key) && !string.IsNullOrWhiteSpace(_value);

        /// <summary>
        /// Destaque de validação visual: Valor está vazio, mas o Nome foi preenchido.
        /// </summary>
        [JsonIgnore]
        public bool IsValueInvalid => !string.IsNullOrWhiteSpace(_key) && string.IsNullOrWhiteSpace(_value);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
