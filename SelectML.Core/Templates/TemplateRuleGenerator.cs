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
        /// Identifica o índice da coluna (0-based) a partir do texto selecionado ou da posição do cursor.
        /// </summary>
        public static int IdentifyColumnIndex(string fullLine, string separator, string selectedText, int selectionStart)
        {
            if (string.IsNullOrEmpty(fullLine) || string.IsNullOrEmpty(separator)) return 0;

            var tokens = fullLine.Split(new[] { separator }, StringSplitOptions.None);
            if (tokens.Length <= 1) return 0;

            // 1. Se selectedText corresponde exatamente a um token único
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                int exactMatchIndex = -1;
                int matchCount = 0;
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (tokens[i].Trim().Trim('"', '\'').Equals(selectedText.Trim().Trim('"', '\''), StringComparison.OrdinalIgnoreCase))
                    {
                        exactMatchIndex = i;
                        matchCount++;
                    }
                }
                if (matchCount == 1) return exactMatchIndex;
            }

            // 2. Se houver cursor/posição de seleção ou múltiplos matches (desambiguação por posição física na linha)
            int currentOffset = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int tokenLength = tokens[i].Length;
                int tokenEnd = currentOffset + tokenLength;

                if (selectionStart >= currentOffset && selectionStart <= tokenEnd)
                {
                    return i;
                }

                currentOffset = tokenEnd + separator.Length;
            }

            // 3. Fallback: se selectedText está contido em algum token
            if (!string.IsNullOrWhiteSpace(selectedText))
            {
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (tokens[i].IndexOf(selectedText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return i;
                    }
                }
            }

            return 0;
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
                    template.DefaultSeparator = separator;
                    break;

                case LineRuleRole.PartName:
                case LineRuleRole.BatchNumber:
                    ConfigureMetadataRule(rule, role, fullLine, selectedText, separator);
                    break;

                case LineRuleRole.RowFeatureColumn:
                {
                    var existingRule = template.LineRules.FirstOrDefault(r => r.Role == LineRuleRole.RowFeatureAndValue);
                    var targetRule = existingRule ?? new TemplateLineRule
                    {
                        LineIndex = 0,
                        Role = LineRuleRole.RowFeatureAndValue,
                        Separator = separator,
                        FeatureColumnIndex = -1,
                        ValueColumnIndex = -1
                    };

                    targetRule.Separator = separator;
                    targetRule.LineIndex = 0;
                    template.DataStartLine = lineIndex;

                    if (!string.IsNullOrEmpty(separator) && fullLine.Contains(separator))
                    {
                        int colIdx = IdentifyColumnIndex(fullLine, separator, selectedText, selectionStart);
                        targetRule.FeatureColumnIndex = colIdx;
                    }
                    else
                    {
                        ConfigureRowRegex(targetRule, "feat", fullLine, selectedText, selectionStart);
                    }

                    return targetRule;
                }

                case LineRuleRole.RowValueColumn:
                {
                    var existingRule = template.LineRules.FirstOrDefault(r => r.Role == LineRuleRole.RowFeatureAndValue);
                    var targetRule = existingRule ?? new TemplateLineRule
                    {
                        LineIndex = 0,
                        Role = LineRuleRole.RowFeatureAndValue,
                        Separator = separator,
                        FeatureColumnIndex = -1,
                        ValueColumnIndex = -1
                    };

                    targetRule.Separator = separator;
                    targetRule.LineIndex = 0;
                    template.DataStartLine = lineIndex;

                    if (!string.IsNullOrEmpty(separator) && fullLine.Contains(separator))
                    {
                        int colIdx = IdentifyColumnIndex(fullLine, separator, selectedText, selectionStart);
                        targetRule.ValueColumnIndex = colIdx;
                    }
                    else
                    {
                        ConfigureRowRegex(targetRule, "val", fullLine, selectedText, selectionStart);
                    }

                    return targetRule;
                }

                case LineRuleRole.RowFeatureAndValue:
                {
                    var existingRule = template.LineRules.FirstOrDefault(r => r.Role == LineRuleRole.RowFeatureAndValue);
                    var targetRule = existingRule ?? new TemplateLineRule
                    {
                        LineIndex = 0,
                        Role = LineRuleRole.RowFeatureAndValue,
                        Separator = separator
                    };

                    ConfigureRowByRowRule(targetRule, lineIndex, fullLine, selectedText, separator, template);
                    return targetRule;
                }
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
                    // Obtém o prefixo imediato dentro do mesmo token (ignorando espaços intermediários)
                    int end = idx - 1;
                    while (end >= 0 && char.IsWhiteSpace(fullLine[end])) end--;

                    if (end >= 0)
                    {
                        int lastDelim = fullLine.LastIndexOfAny(SeparatorCandidates, end);
                        string prefix = lastDelim >= 0 
                            ? fullLine.Substring(lastDelim + 1, end - lastDelim).Trim() 
                            : fullLine.Substring(0, end + 1).Trim();

                        if (!string.IsNullOrEmpty(prefix) && (prefix.EndsWith(":") || prefix.EndsWith("=") || prefix.Length <= 30))
                        {
                            rule.RegexPattern = $@"{Regex.Escape(prefix)}\s*(?<{groupName}>[^\r\n;\t,]+)";
                            rule.RegexGroupName = groupName;
                            return;
                        }
                    }
                }

                // Captura do padrão exato selecionado
                rule.RegexPattern = $@"(?<{groupName}>{Regex.Escape(selectedText)})";
                rule.RegexGroupName = groupName;
            }
        }

        private static void ConfigureRowRegex(
            TemplateLineRule rule, 
            string groupName, 
            string fullLine, 
            string selectedText, 
            int selectionStart)
        {
            string targetText = !string.IsNullOrWhiteSpace(selectedText) ? selectedText : string.Empty;
            int idx = !string.IsNullOrWhiteSpace(targetText) ? fullLine.IndexOf(targetText, StringComparison.Ordinal) : selectionStart;
            if (idx < 0) idx = 0;

            string pattern = string.Empty;

            // Procura por um prefixo (rótulo, ex: "FEAT:", "MEAS:", "VAL:") antes do texto selecionado
            if (idx > 0)
            {
                int end = idx - 1;
                while (end >= 0 && char.IsWhiteSpace(fullLine[end])) end--;

                if (end >= 0)
                {
                    int lastDelim = fullLine.LastIndexOfAny(new[] { ' ', '\t', ';', ',', '|' }, end);
                    string prefix = lastDelim >= 0
                        ? fullLine.Substring(lastDelim + 1, end - lastDelim).Trim()
                        : fullLine.Substring(0, end + 1).Trim();

                    if (!string.IsNullOrEmpty(prefix) && (prefix.EndsWith(":") || prefix.EndsWith("=") || prefix.Length <= 15))
                    {
                        if (groupName == "feat")
                        {
                            pattern = $@"{Regex.Escape(prefix)}\s*(?<feat>[A-Za-z0-9_Ø°µ\.\-]+)";
                        }
                        else
                        {
                            pattern = $@"{Regex.Escape(prefix)}\s*(?<val>[-+]?\d+[\.,]?\d*)";
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(pattern))
            {
                if (groupName == "feat")
                {
                    pattern = !string.IsNullOrWhiteSpace(targetText) 
                        ? $@"(?<feat>{Regex.Escape(targetText)})" 
                        : @"(?<feat>[A-Za-z0-9_Ø°µ\.\-]+)";
                }
                else
                {
                    pattern = @"(?<val>[-+]?\d+[\.,]?\d*)";
                }
            }

            // Mesclagem com padrão já existente se houver
            if (string.IsNullOrWhiteSpace(rule.RegexPattern))
            {
                rule.RegexPattern = pattern;
            }
            else
            {
                bool hasFeat = rule.RegexPattern.Contains("(?<feat>");
                bool hasVal = rule.RegexPattern.Contains("(?<val>");

                if (groupName == "feat")
                {
                    if (hasVal)
                    {
                        rule.RegexPattern = pattern + ".*?" + ExtractGroupPattern(rule.RegexPattern, "val");
                    }
                    else
                    {
                        rule.RegexPattern = pattern;
                    }
                }
                else if (groupName == "val")
                {
                    if (hasFeat)
                    {
                        rule.RegexPattern = ExtractGroupPattern(rule.RegexPattern, "feat") + ".*?" + pattern;
                    }
                    else
                    {
                        rule.RegexPattern = pattern;
                    }
                }
            }

            rule.RegexGroupName = string.Empty;
        }

        private static string ExtractGroupPattern(string fullRegex, string group)
        {
            if (string.IsNullOrWhiteSpace(fullRegex))
                return group == "feat" ? @"(?<feat>[A-Za-z0-9_Ø°µ\.\-]+)" : @"(?<val>[-+]?\d+[\.,]?\d*)";

            int sepIdx = fullRegex.IndexOf(".*?", StringComparison.Ordinal);

            if (group == "feat")
            {
                if (sepIdx > 0)
                {
                    return fullRegex.Substring(0, sepIdx);
                }
                return fullRegex;
            }
            else // "val"
            {
                if (sepIdx >= 0)
                {
                    return fullRegex.Substring(sepIdx + 3);
                }
                return fullRegex;
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
            rule.LineIndex = 0;
            template.DataStartLine = lineIndex;

            var tokens = fullLine.Split(new[] { separator }, StringSplitOptions.None);

            if (tokens.Length >= 2)
            {
                int featureCol = 0;
                int valueCol = 1;

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
                case LineRuleRole.RowFeatureColumn:
                case LineRuleRole.RowValueColumn:
                    template.LineRules.RemoveAll(r => 
                        r.Role == LineRuleRole.CharacteristicNames || 
                        r.Role == LineRuleRole.MeasuredValues);

                    newRule.Role = LineRuleRole.RowFeatureAndValue;
                    newRule.LineIndex = 0;

                    if (!template.LineRules.Contains(newRule))
                    {
                        template.LineRules.RemoveAll(r => r.Role == LineRuleRole.RowFeatureAndValue);
                        template.LineRules.Add(newRule);
                    }
                    return;

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
                    newRule.Role == LineRuleRole.MeasuredValues)
                {
                    template.LineRules.RemoveAll(r => r.LineIndex == newRule.LineIndex);
                }
                else
                {
                    template.LineRules.RemoveAll(r => r.LineIndex == newRule.LineIndex && 
                        (r.Role == LineRuleRole.CharacteristicNames || 
                         r.Role == LineRuleRole.MeasuredValues || 
                         r.Role == newRule.Role));
                }
            }

            if (!template.LineRules.Contains(newRule))
            {
                template.LineRules.Add(newRule);
            }
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
