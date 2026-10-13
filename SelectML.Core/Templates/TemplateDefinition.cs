using System;
using System.Collections.Generic;
using System.Linq;

namespace SelectML.Core.Templates
{
    /// <summary>
    /// Representa a definição completa de um template para o Leitor de Arquivos de Texto.
    /// </summary>
    public class TemplateDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Nome amigável do template (ex: "Torno CNC", "Mitutoyo LH-600", "Vici CSV").
        /// </summary>
        public string Name { get; set; } = "Novo Template";

        /// <summary>
        /// Extensão dos arquivos que este template deve ler (ex: ".csv", ".txt", ".dat", "*.*").
        /// </summary>
        public string FileExtension { get; set; } = ".csv";

        /// <summary>
        /// Caso verdadeiro, utiliza o nome do arquivo como Nome da Rotina (PartName) caso não esteja demarcado no texto.
        /// </summary>
        public bool UseFileNameForPartName { get; set; } = true;

        /// <summary>
        /// Caso verdadeiro, utiliza o nome do arquivo como Número de Lote (BatchNumber) caso não esteja demarcado no texto.
        /// </summary>
        public bool UseFileNameForBatchNumber { get; set; } = true;

        /// <summary>
        /// Separador de colunas padrão para o arquivo (ex: ";", ",", "\t").
        /// </summary>
        public string DefaultSeparator { get; set; } = ";";

        /// <summary>
        /// Linha inicial onde os dados começam em arquivos com formato linha a linha (1-based).
        /// </summary>
        public int DataStartLine { get; set; } = 1;

        /// <summary>
        /// Lista de regras de linha configuradas pelo operador.
        /// </summary>
        public List<TemplateLineRule> LineRules { get; set; } = new List<TemplateLineRule>();

        /// <summary>
        /// Valida se o template possui a estrutura mínima necessária para realizar uma medição válida.
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Name))
            {
                errorMessage = "O nome do template é obrigatório.";
                return false;
            }

            bool hasFeatures = LineRules.Any(r => r.Role == LineRuleRole.CharacteristicNames || r.Role == LineRuleRole.RowFeatureAndValue);
            bool hasValues = LineRules.Any(r => r.Role == LineRuleRole.MeasuredValues || r.Role == LineRuleRole.RowFeatureAndValue);

            if (!hasFeatures)
            {
                errorMessage = "O template precisa conter o mapeamento de Características (Nomes das cotas).";
                return false;
            }

            if (!hasValues)
            {
                errorMessage = "O template precisa conter o mapeamento de Valores Medidos.";
                return false;
            }

            return true;
        }
    }
}
