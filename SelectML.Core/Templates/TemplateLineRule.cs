using System;

namespace SelectML.Core.Templates
{
    /// <summary>
    /// Identifica o papel/função desempenhado por uma linha ou trecho dentro do arquivo de medição.
    /// </summary>
    public enum LineRuleRole
    {
        /// <summary>
        /// Nome da Rotina / Identificação da Peça (ex: ILCM3810-US).
        /// </summary>
        PartName = 1,

        /// <summary>
        /// Nome da Corrida / Número de Lote (ex: 067336-092026).
        /// </summary>
        BatchNumber = 2,

        /// <summary>
        /// Linha contendo múltiplos nomes de características delimitadas por separador (ex: MICL 1.96;MICL Ø2.32).
        /// </summary>
        CharacteristicNames = 3,

        /// <summary>
        /// Linha contendo múltiplos valores medidos delimitados por separador (ex: 10.5984;0.0336).
        /// </summary>
        MeasuredValues = 4,

        /// <summary>
        /// Linha que contém tanto o nome da característica quanto o valor medido correspondente (formato vertical / linha por linha).
        /// </summary>
        RowFeatureAndValue = 5,

        /// <summary>
        /// Coluna ou trecho que contém o nome da característica no formato linha a linha.
        /// </summary>
        RowFeatureColumn = 6,

        /// <summary>
        /// Coluna ou trecho que contém o valor medido no formato linha a linha.
        /// </summary>
        RowValueColumn = 7
    }

    /// <summary>
    /// Regra de interpretação para uma linha ou trecho do arquivo de texto.
    /// </summary>
    public class TemplateLineRule
    {
        /// <summary>
        /// Linha do arquivo (1-based, ex: 1, 2, 3...). 
        /// Caso seja 0, aplica-se como regra repetitiva contínua para todas as linhas de dados a partir de DataStartLine.
        /// </summary>
        public int LineIndex { get; set; } = 1;

        /// <summary>
        /// Papel da linha/trecho no modelo de medição.
        /// </summary>
        public LineRuleRole Role { get; set; }

        /// <summary>
        /// Separador de colunas utilizado nesta linha (ex: ";", ",", "\t", " ").
        /// </summary>
        public string Separator { get; set; } = ";";

        /// <summary>
        /// Índice da coluna (0-based) que contém o nome da característica em formato linha por linha.
        /// </summary>
        public int FeatureColumnIndex { get; set; } = 0;

        /// <summary>
        /// Índice da coluna (0-based) que contém o valor medido em formato linha por linha.
        /// </summary>
        public int ValueColumnIndex { get; set; } = 1;

        /// <summary>
        /// Expressão regular opcional para captura de padrão ou validação da linha.
        /// </summary>
        public string RegexPattern { get; set; } = string.Empty;

        /// <summary>
        /// Nome do grupo nomeado de captura no Regex (ex: "val", "part", "batch").
        /// </summary>
        public string RegexGroupName { get; set; } = string.Empty;

        /// <summary>
        /// Descrição amigável do papel para exibição em listas e tabelas.
        /// </summary>
        public string RoleDescription => Role switch
        {
            LineRuleRole.PartName => "Nome da Rotina / Peça",
            LineRuleRole.BatchNumber => "Nome da Corrida / Lote",
            LineRuleRole.CharacteristicNames => "Características (Nomes)",
            LineRuleRole.MeasuredValues => "Valores Medidos",
            LineRuleRole.RowFeatureAndValue => "Cota + Valor (Linha a Linha)",
            LineRuleRole.RowFeatureColumn => "Coluna de Característica",
            LineRuleRole.RowValueColumn => "Coluna de Valor Medido",
            _ => Role.ToString()
        };

        /// <summary>
        /// Rótulo amigável da linha aplicada.
        /// </summary>
        public string LineDisplay => LineIndex == 0 ? "Linha a Linha" : $"Linha {LineIndex}";

        /// <summary>
        /// Resumo dos parâmetros aplicados nesta regra.
        /// </summary>
        public string DetailsDisplay
        {
            get
            {
                if (Role == LineRuleRole.CharacteristicNames || Role == LineRuleRole.MeasuredValues)
                    return $"Separador: '{Separator}'";
                if (Role == LineRuleRole.RowFeatureAndValue)
                {
                    if (!string.IsNullOrWhiteSpace(RegexPattern))
                        return $"Regex: {RegexPattern}";

                    string featText = FeatureColumnIndex >= 0 ? $"Col. {FeatureColumnIndex + 1}" : "(Pendente)";
                    string valText = ValueColumnIndex >= 0 ? $"Col. {ValueColumnIndex + 1}" : "(Pendente)";
                    return $"Cota: {featText}, Valor: {valText} (Sep: '{Separator}')";
                }
                if (Role == LineRuleRole.PartName || Role == LineRuleRole.BatchNumber)
                {
                    if (!string.IsNullOrWhiteSpace(RegexPattern))
                        return $"Regex: {RegexPattern}";
                    if (!string.IsNullOrEmpty(Separator))
                        return $"Col. {FeatureColumnIndex + 1} (Sep: '{Separator}')";
                }
                if (!string.IsNullOrWhiteSpace(RegexPattern))
                    return $"Regex: {RegexPattern}";
                return "Linha inteira / Padrão";
            }
        }
    }
}
