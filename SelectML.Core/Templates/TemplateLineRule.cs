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
        RowFeatureAndValue = 5
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
    }
}
