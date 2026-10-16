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
            _key = Sanitize(key);
            _value = Sanitize(value);
        }

        /// <summary>
        /// Nome / Identificador da rastreabilidade (ex: "Série", "Operador", "Turno").
        /// </summary>
        public string Key
        {
            get => _key;
            set
            {
                var sanitized = Sanitize(value);
                if (_key != sanitized)
                {
                    _key = sanitized;
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
                var sanitized = Sanitize(value);
                if (_value != sanitized)
                {
                    _value = sanitized;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsComplete));
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(IsKeyInvalid));
                    OnPropertyChanged(nameof(IsValueInvalid));
                }
            }
        }

        private static string Sanitize(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Replace(";", string.Empty)
                       .Replace("|", string.Empty)
                       .Replace("\r", string.Empty)
                       .Replace("\n", string.Empty);
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
