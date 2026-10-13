using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SelectML.Core.Templates
{
    /// <summary>
    /// Representa a definição completa de um template para o Leitor de Arquivos de Texto.
    /// </summary>
    public class TemplateDefinition : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _id = Guid.NewGuid().ToString();
        private string _name = "Novo Template";
        private string _fileExtension = ".csv";
        private bool _useFileNameForPartName = true;
        private bool _useFileNameForBatchNumber = true;
        private string _defaultSeparator = ";";
        private int _dataStartLine = 1;
        private List<TemplateLineRule> _lineRules = new List<TemplateLineRule>();

        public string Id 
        { 
            get => _id; 
            set { if (_id != value) { _id = value; OnPropertyChanged(); } } 
        }

        /// <summary>
        /// Nome amigável do template (ex: "Torno CNC", "Mitutoyo LH-600", "Vici CSV").
        /// </summary>
        public string Name 
        { 
            get => _name; 
            set { if (_name != value) { _name = value; OnPropertyChanged(); } } 
        }

        /// <summary>
        /// Extensão dos arquivos que este template deve ler (ex: ".csv", ".txt", ".dat", "*.*").
        /// </summary>
        public string FileExtension 
        { 
            get => _fileExtension; 
            set { if (_fileExtension != value) { _fileExtension = value; OnPropertyChanged(); } } 
        }

        /// <summary>
        /// Caso verdadeiro, utiliza o nome do arquivo como Nome da Rotina (PartName) caso não esteja demarcado no texto.
        /// </summary>
        public bool UseFileNameForPartName 
        { 
            get => _useFileNameForPartName; 
            set { if (_useFileNameForPartName != value) { _useFileNameForPartName = value; OnPropertyChanged(); } } 
        }

        /// <summary>
        /// Caso verdadeiro, utiliza o nome do arquivo como Número de Lote (BatchNumber) caso não esteja demarcado no texto.
        /// </summary>
        public bool UseFileNameForBatchNumber 
        { 
            get => _useFileNameForBatchNumber; 
            set { if (_useFileNameForBatchNumber != value) { _useFileNameForBatchNumber = value; OnPropertyChanged(); } } 
        }

        /// <summary>
        /// Separador de colunas padrão para o arquivo (ex: ";", ",", "\t").
        /// </summary>
        public string DefaultSeparator 
        { 
            get => _defaultSeparator; 
            set { if (_defaultSeparator != value) { _defaultSeparator = value; OnPropertyChanged(); } } 
        }

        /// <summary>
        /// Linha inicial onde os dados começam em arquivos com formato linha a linha (1-based).
        /// </summary>
        public int DataStartLine 
        { 
            get => _dataStartLine; 
            set { if (_dataStartLine != value) { _dataStartLine = value; OnPropertyChanged(); } } 
        }

        /// <summary>
        /// Lista de regras de linha configuradas pelo operador.
        /// </summary>
        public List<TemplateLineRule> LineRules 
        { 
            get => _lineRules; 
            set { if (_lineRules != value) { _lineRules = value; OnPropertyChanged(); } } 
        }

        /// <summary>
        /// Valida se o template possui a estrutura mínima necessária para realizar uma medição válida.
        /// Caso Rotina ou Corrida não estejam mapeadas no texto, ativa automaticamente o fallback para o nome do arquivo.
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Name))
            {
                errorMessage = "O nome do template é obrigatório.";
                return false;
            }

            bool hasFeatures = LineRules.Any(r => 
                r.Role == LineRuleRole.CharacteristicNames || 
                (r.Role == LineRuleRole.RowFeatureAndValue && (r.FeatureColumnIndex >= 0 || (!string.IsNullOrEmpty(r.RegexPattern) && r.RegexPattern.Contains("(?<feat>")))));

            bool hasValues = LineRules.Any(r => 
                r.Role == LineRuleRole.MeasuredValues || 
                (r.Role == LineRuleRole.RowFeatureAndValue && (r.ValueColumnIndex >= 0 || (!string.IsNullOrEmpty(r.RegexPattern) && r.RegexPattern.Contains("(?<val>")))));

            bool hasRowByRow = LineRules.Any(r => r.Role == LineRuleRole.RowFeatureAndValue);

            if (!hasFeatures && !hasValues)
            {
                errorMessage = "O template precisa conter o mapeamento de Características e Valores Medidos.";
                return false;
            }

            if (!hasFeatures)
            {
                errorMessage = hasRowByRow
                    ? "O formato linha a linha necessita da definição da coluna ou trecho das Características (Nomes das cotas)."
                    : "O template precisa conter o mapeamento de Características (Nomes das cotas).";
                return false;
            }

            if (!hasValues)
            {
                errorMessage = hasRowByRow
                    ? "O formato linha a linha necessita da definição da coluna ou trecho dos Valores Medidos."
                    : "O template precisa conter o mapeamento de Valores Medidos.";
                return false;
            }

            // Regras de fallback automático: se a rotina ou a corrida não foram mapeadas no texto,
            // ativa automaticamente o fallback para o nome do arquivo.
            UseFileNameForPartName = !LineRules.Any(r => r.Role == LineRuleRole.PartName);
            UseFileNameForBatchNumber = !LineRules.Any(r => r.Role == LineRuleRole.BatchNumber);

            return true;
        }
    }
}
