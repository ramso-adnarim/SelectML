using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SelectML.Core
{
    /// <summary>
    /// Representa um par de dados de rastreabilidade (Nome e Valor)
    /// cadastrado pelo usuário para injeção no CSV final enviado ao MeasurLink.
    /// Suporta integração reativa com definições de catálogo, opções pré-definidas e validações Regex.
    /// </summary>
    public class TraceabilityItem : INotifyPropertyChanged
    {
        private static readonly List<string> s_emptyOptions = new();

        /// <summary>
        /// Provedor estático para resolução de definições de catálogo por nome.
        /// </summary>
        public static Func<string, TraceabilityFieldDefinition?>? CatalogResolver { get; set; }

        private string _key = string.Empty;
        private string _value = string.Empty;
        private TraceabilityFieldDefinition? _definition;
        private bool _isUnmappedInRun;

        public TraceabilityItem()
        {
        }

        public TraceabilityItem(string key, string value)
        {
            _key = Sanitize(key);
            _value = Sanitize(value);
            RefreshDefinition();
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
                    RefreshDefinition();
                    OnPropertyChanged();
                    NotifyAllStateChanged();
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
                    NotifyAllStateChanged();
                }
            }
        }

        /// <summary>
        /// Atualiza o vínculo com a definição cadastrada no banco MeasurLink correspondente ao nome atual.
        /// </summary>
        public void RefreshDefinition()
        {
            _definition = CatalogResolver?.Invoke(_key);

            // Regra P2: Se o campo for estrito (ItemType = 1), limpa o valor se não fizer parte da lista oficial
            if (_definition != null && _definition.IsStrictSelection)
            {
                if (!string.IsNullOrWhiteSpace(_value) && !_definition.PredefinedOptions.Any(o => string.Equals(o, _value, StringComparison.OrdinalIgnoreCase)))
                {
                    _value = string.Empty;
                    OnPropertyChanged(nameof(Value));
                }
            }

            OnPropertyChanged(nameof(Definition));
            OnPropertyChanged(nameof(HasPredefinedOptions));
            OnPropertyChanged(nameof(PredefinedOptions));
            OnPropertyChanged(nameof(ItemType));
            OnPropertyChanged(nameof(IsStrictSelection));
            OnPropertyChanged(nameof(IsSelectionWithAdditions));
            OnPropertyChanged(nameof(SampleMask));
            NotifyAllStateChanged();
        }

        /// <summary>
        /// Definição de catálogo associada a este campo no MeasurLink (null se for campo customizado).
        /// </summary>
        [JsonIgnore]
        public TraceabilityFieldDefinition? Definition => _definition;

        /// <summary>
        /// Indica se há opções pré-definidas para seleção na coluna Valor.
        /// </summary>
        [JsonIgnore]
        public bool HasPredefinedOptions => _definition != null && _definition.HasPredefinedOptions;

        /// <summary>
        /// Opções pré-definidas do MeasurLink para exibição no ComboBox de Valor.
        /// </summary>
        [JsonIgnore]
        public List<string> PredefinedOptions => _definition?.PredefinedOptions ?? s_emptyOptions;

        /// <summary>
        /// ItemType do MeasurLink (1 = Estrito, 2 = Com Adição, 3 = Texto/Máscara).
        /// </summary>
        [JsonIgnore]
        public int ItemType => _definition?.ItemType ?? 3;

        /// <summary>
        /// Se true, operador só pode selecionar itens da lista oficial (ComboBox fechado).
        /// </summary>
        [JsonIgnore]
        public bool IsStrictSelection => _definition != null && _definition.IsStrictSelection;

        /// <summary>
        /// Se true, operador pode escolher da lista ou digitar livremente (ComboBox aberto).
        /// </summary>
        [JsonIgnore]
        public bool IsSelectionWithAdditions => _definition != null && _definition.IsSelectionWithAdditions;

        /// <summary>
        /// Texto de exemplo/máscara do MeasurLink (ex: "0000", "00'00''") para placeholder visual.
        /// </summary>
        [JsonIgnore]
        public string? SampleMask => _definition?.Mask?.SampleMask;

        /// <summary>
        /// Indica se o valor digitado desrespeita a máscara Regex configurada no MeasurLink.
        /// </summary>
        [JsonIgnore]
        public bool IsMaskInvalid
        {
            get
            {
                if (_definition?.Mask != null && _definition.Mask.IsEnabled && !string.IsNullOrWhiteSpace(_value))
                {
                    return !_definition.Mask.Validate(_value);
                }
                return false;
            }
        }

        /// <summary>
        /// Indica se a rastreabilidade preenchida não faz parte das regras mapeadas na corrida ativa.
        /// </summary>
        [JsonIgnore]
        public bool IsUnmappedInRun
        {
            get => _isUnmappedInRun;
            set
            {
                if (_isUnmappedInRun != value)
                {
                    _isUnmappedInRun = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasWarning));
                    OnPropertyChanged(nameof(ValidationToolTip));
                }
            }
        }

        /// <summary>
        /// Indica se há erro impeditivo de preenchimento ou formato (Borda Vermelha - Prioridade 1).
        /// </summary>
        [JsonIgnore]
        public bool HasError => IsKeyInvalid || IsValueInvalid || IsMaskInvalid;

        /// <summary>
        /// Indica advertência de corrida não mapeada (Borda Âmbar/Amarela - Prioridade 2).
        /// </summary>
        [JsonIgnore]
        public bool HasWarning => !HasError && IsComplete && IsUnmappedInRun;

        /// <summary>
        /// Mensagem de orientação para o operador via ToolTip dinâmico.
        /// </summary>
        [JsonIgnore]
        public string? ValidationToolTip
        {
            get
            {
                if (IsKeyInvalid || IsValueInvalid)
                    return "Preenchimento obrigatório.";

                if (IsMaskInvalid)
                {
                    string sample = !string.IsNullOrEmpty(SampleMask) ? SampleMask : _definition?.Mask?.RegexPattern ?? "";
                    return $"Formato exigido: {sample}";
                }

                if (HasWarning)
                    return "Esta rastreabilidade não está mapeada nesta corrida no MeasurLink.";

                return null;
            }
        }

        /// <summary>
        /// Indica se ambos os campos (Nome E Valor) estão preenchidos.
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

        private void NotifyAllStateChanged()
        {
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsKeyInvalid));
            OnPropertyChanged(nameof(IsValueInvalid));
            OnPropertyChanged(nameof(IsMaskInvalid));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasWarning));
            OnPropertyChanged(nameof(ValidationToolTip));
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
