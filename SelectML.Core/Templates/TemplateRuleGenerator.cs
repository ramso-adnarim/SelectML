using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SelectML.Core.Templates
{
    /// <summary>
    /// Motor inteligente responsável por gerar, deduzir e aplicar regras de template
    /// a partir de seleções interativas feitas pelo operador no arquivo de texto.
    /// </summary>
    public static class TemplateRuleGenerator
    {
        private static readonly char[] SeparatorCandidates = new[] { ';', ',', '\t', '|' };

        /// <summary>
        /// Detecta automaticamente o separador predominante em uma linha.
        /// </summary>
        public static string DetectSeparator(string line, string fallback = ";")
        {
            if (string.IsNullOrEmpty(line)) return fallback;

            char bestChar = fallback[0];
            int bestCount = 0;

            foreach (var candidate in SeparatorCandidates)
            {
                int count = line.Count(c => c == candidate);
                if (count > bestCount)
                {
                    bestCount = count;
                    bestChar = candidate;
                }
            }

            return bestCount > 0 ? bestChar.ToString() : fallback;
        }

        /// <summary>
        /// Gera uma regra de linha com base no papel selecionado e no contexto da linha/seleção.
        /// </summary>
        public static TemplateLineRule GenerateRule(
            LineRuleRole role, 
            int lineIndex, 
            string fullLine, 
            string? selectedText, 
            int selectionStart, 
            int selectionLength, 
            TemplateDefinition template)
        {
            fullLine = fullLine ?? string.Empty;
            selectedText = selectedText?.Trim() ?? string.Empty;

            string separator = DetectSeparator(fullLine, template.DefaultSeparator);

            var rule = new TemplateLineRule
            {
                LineIndex = lineIndex,
                Role = role,
                Separator = separator
            };

            switch (role)
            {
                case LineRuleRole.CharacteristicNames:
                case LineRuleRole.MeasuredValues:
                    // Linhas de matriz horizontal: armazenam o separador detectado
                    template.DefaultSeparator = separator;
                    break;

                case LineRuleRole.PartName:
                case LineRuleRole.BatchNumber:
                    ConfigureMetadataRule(rule, role, fullLine, selectedText, separator);
                    break;

                case LineRuleRole.RowFeatureAndValue:
                    ConfigureRowByRowRule(rule, lineIndex, fullLine, selectedText, separator, template);
                    break;
            }

            return rule;
        }

        private static void ConfigureMetadataRule(
            TemplateLineRule rule, 
            LineRuleRole role, 
            string fullLine, 
            string selectedText, 
            string separator)
        {
            string groupName = role == LineRuleRole.PartName ? "part" : "batch";

            // 1. Se a linha tem delimitadores e o texto selecionado corresponde a uma coluna
            if (!string.IsNullOrEmpty(separator) && fullLine.Contains(separator))
            {
                var tokens = fullLine.Split(new[] { separator }, StringSplitOptions.None);
                for (int i = 0; i < tokens.Length; i++)
                {
                    string tokenTrimmed = tokens[i].Trim().Trim('"', '\'');
                    if (!string.IsNullOrEmpty(selectedText) && tokenTrimmed.Equals(selectedText, StringComparison.OrdinalIgnoreCase))
                    {
                        rule.FeatureColumnIndex = i;
                        rule.Separator = separator;
                        return;
                    }
                }
            }

            // 2. Se há texto selecionado específico em relação à linha inteira
            if (!string.IsNullOrEmpty(selectedText) && selectedText != fullLine.Trim())
            {
                int idx = fullLine.IndexOf(selectedText, StringComparison.Ordinal);
                if (idx > 0)
                {
                    // Obtém o prefixo imediato dentro do mesmo token (após o último separador)
                    int lastDelim = fullLine.LastIndexOfAny(SeparatorCandidates, idx - 1);
                    string prefix = lastDelim >= 0 
                        ? fullLine.Substring(lastDelim + 1, idx - lastDelim - 1).Trim() 
                        : fullLine.Substring(0, idx).Trim();

                    if (!string.IsNullOrEmpty(prefix) && (prefix.EndsWith(":") || prefix.EndsWith("=") || prefix.Length <= 30))
                    {
                        rule.RegexPattern = $@"{Regex.Escape(prefix)}\s*(?<{groupName}>[^\r\n;\t,]+)";
                        rule.RegexGroupName = groupName;
                        return;
                    }
                }

                // Captura do padrão exato selecionado
                rule.RegexPattern = $@"(?<{groupName}>{Regex.Escape(selectedText)})";
                rule.RegexGroupName = groupName;
            }
        }

        private static void ConfigureRowByRowRule(
            TemplateLineRule rule, 
            int lineIndex, 
            string fullLine, 
            string selectedText, 
            string separator, 
            TemplateDefinition template)
        {
            // Formato Linha por Linha: aplica-se repetitivamente para todas as linhas a partir desta
            rule.LineIndex = 0;
            template.DataStartLine = lineIndex;

            var tokens = fullLine.Split(new[] { separator }, StringSplitOptions.None);

            if (tokens.Length >= 2)
            {
                int featureCol = 0;
                int valueCol = 1;

                // Identifica qual token é numérico
                bool token0IsNum = IsNumeric(tokens[0]);
                bool token1IsNum = IsNumeric(tokens[1]);

                if (token0IsNum && !token1IsNum)
                {
                    valueCol = 0;
                    featureCol = 1;
                }
                else if (!token0IsNum && token1IsNum)
                {
                    featureCol = 0;
                    valueCol = 1;
                }
                else if (tokens.Length > 2)
                {
                    // Se houver mais de 2 colunas, busca a primeira coluna que seja número
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        if (IsNumeric(tokens[i]))
                        {
                            valueCol = i;
                            break;
                        }
                    }
                }

                rule.FeatureColumnIndex = featureCol;
                rule.ValueColumnIndex = valueCol;
            }
            else
            {
                // Fallback padrão se não houver múltiplos tokens
                rule.FeatureColumnIndex = 0;
                rule.ValueColumnIndex = 1;
            }
        }

        private static bool IsNumeric(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string cleaned = raw.Trim().Trim('"', '\'').Replace(',', '.');
            return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }

        /// <summary>
        /// Aplica uma regra ao template, resolvendo sobreposições e garantindo consistência estrutural.
        /// </summary>
        public static void ApplyRule(TemplateDefinition template, TemplateLineRule newRule)
        {
            if (template == null || newRule == null) return;

            // Remove regras conflitantes por papel
            switch (newRule.Role)
            {
                case LineRuleRole.CharacteristicNames:
                    template.LineRules.RemoveAll(r => r.Role == LineRuleRole.CharacteristicNames || r.Role == LineRuleRole.RowFeatureAndValue);
                    break;

                case LineRuleRole.MeasuredValues:
                    template.LineRules.RemoveAll(r => r.Role == LineRuleRole.MeasuredValues || r.Role == LineRuleRole.RowFeatureAndValue);
                    break;

                case LineRuleRole.RowFeatureAndValue:
                    // Linha por linha substitui o layout horizontal e outras regras de linha por linha
                    template.LineRules.RemoveAll(r => 
                        r.Role == LineRuleRole.CharacteristicNames || 
                        r.Role == LineRuleRole.MeasuredValues || 
                        r.Role == LineRuleRole.RowFeatureAndValue);
                    break;

                case LineRuleRole.PartName:
                    template.LineRules.RemoveAll(r => r.Role == LineRuleRole.PartName);
                    break;

                case LineRuleRole.BatchNumber:
                    template.LineRules.RemoveAll(r => r.Role == LineRuleRole.BatchNumber);
                    break;
            }

            // Remove regras na mesma linha física apenas se for regra de linha inteira ou do mesmo papel
            if (newRule.LineIndex > 0)
            {
                if (newRule.Role == LineRuleRole.CharacteristicNames || 
                    newRule.Role == LineRuleRole.MeasuredValues || 
                    newRule.Role == LineRuleRole.RowFeatureAndValue)
                {
                    template.LineRules.RemoveAll(r => r.LineIndex == newRule.LineIndex);
                }
                else
                {
                    template.LineRules.RemoveAll(r => r.LineIndex == newRule.LineIndex && 
                        (r.Role == LineRuleRole.CharacteristicNames || 
                         r.Role == LineRuleRole.MeasuredValues || 
                         r.Role == LineRuleRole.RowFeatureAndValue || 
                         r.Role == newRule.Role));
                }
            }

            template.LineRules.Add(newRule);
        }

        /// <summary>
        /// Remove qualquer regra associada a uma linha física específica.
        /// </summary>
        public static void RemoveRulesForLine(TemplateDefinition template, int lineIndex)
        {
            if (template == null) return;

            template.LineRules.RemoveAll(r => 
                r.LineIndex == lineIndex || 
                (r.LineIndex == 0 && template.DataStartLine == lineIndex));
        }
    }
}
