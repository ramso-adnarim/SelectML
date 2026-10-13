using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SelectML.Core;
using SelectML.Core.Templates;

namespace SelectML.Parsers.TextTemplate
{
    /// <summary>
    /// Motor determinístico e otimizado de execução de templates de texto.
    /// Realiza a extração dos dados de medição com auto-detecção de encoding,
    /// suporte unificado para formatos horizontal e linha a linha, e conversão decimal inteligente.
    /// </summary>
    public class TemplateExecutionEngine
    {
        public MeasurementData Execute(string filePath, TemplateDefinition template)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Arquivo de medição não encontrado: {filePath}");
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            var data = new MeasurementData();
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

            // 1. Detectar codificação automaticamente (UTF-8 vs Latin-1)
            var encoding = EncodingDetector.Detect(filePath);
            var lines = File.ReadAllLines(filePath, encoding);

            if (lines.Length == 0)
                throw new InvalidOperationException("O arquivo de medição está vazio.");

            // Data da medição (padrão: data de modificação do arquivo ou DateTime.Now)
            try
            {
                data.MeasureDate = File.GetLastWriteTime(filePath);
            }
            catch
            {
                data.MeasureDate = DateTime.Now;
            }

            // 2. Extração de Nome da Rotina (PartName)
            var partRule = template.LineRules.FirstOrDefault(r => r.Role == LineRuleRole.PartName);
            if (partRule != null && partRule.LineIndex > 0 && partRule.LineIndex <= lines.Length)
            {
                data.PartName = ExtractStringFromLine(lines[partRule.LineIndex - 1], partRule);
            }

            if (string.IsNullOrWhiteSpace(data.PartName))
            {
                // Fallback automático padrão para o nome do arquivo caso não demarcado no template
                var parts = fileNameWithoutExt.Split('_');
                data.PartName = parts.Length > 0 ? parts[0].Trim() : fileNameWithoutExt;
            }

            // 3. Extração de Nome da Corrida (BatchNumber / Lote)
            var batchRule = template.LineRules.FirstOrDefault(r => r.Role == LineRuleRole.BatchNumber);
            if (batchRule != null && batchRule.LineIndex > 0 && batchRule.LineIndex <= lines.Length)
            {
                data.BatchNumber = ExtractStringFromLine(lines[batchRule.LineIndex - 1], batchRule);
            }

            if (string.IsNullOrWhiteSpace(data.BatchNumber))
            {
                // Fallback automático padrão para o nome do arquivo caso não demarcado no template
                var parts = fileNameWithoutExt.Split('_');
                data.BatchNumber = parts.Length > 1 ? parts[1].Trim() : fileNameWithoutExt;
            }

            // 4. Extração de Características e Leituras (Formato Horizontal vs Linha a Linha)
            var charRule = template.LineRules.FirstOrDefault(r => r.Role == LineRuleRole.CharacteristicNames);
            var valRule = template.LineRules.FirstOrDefault(r => r.Role == LineRuleRole.MeasuredValues);

            if (charRule != null && valRule != null)
            {
                // Cenário A: Formato Horizontal (Matriz) - Linha de Características + Linha de Valores
                ExtractHorizontalResults(lines, charRule, valRule, data.Results);
            }
            else
            {
                // Cenário B: Formato Linha por Linha (RowFeatureAndValue)
                var rowRules = template.LineRules.Where(r => r.Role == LineRuleRole.RowFeatureAndValue).ToList();
                if (rowRules.Count > 0)
                {
                    ExtractRowByRowResults(lines, rowRules, template, data.Results);
                }
            }

            return data;
        }

        private void ExtractHorizontalResults(string[] lines, TemplateLineRule charRule, TemplateLineRule valRule, Dictionary<string, double> results)
        {
            if (charRule.LineIndex <= 0 || charRule.LineIndex > lines.Length) return;
            if (valRule.LineIndex <= 0 || valRule.LineIndex > lines.Length) return;

            string charLine = lines[charRule.LineIndex - 1];
            string valLine = lines[valRule.LineIndex - 1];

            string charSep = !string.IsNullOrEmpty(charRule.Separator) ? charRule.Separator : ";";
            string valSep = !string.IsNullOrEmpty(valRule.Separator) ? valRule.Separator : charSep;

            var charTokens = SplitTokens(charLine, charSep);
            var valTokens = SplitTokens(valLine, valSep);

            int count = Math.Min(charTokens.Length, valTokens.Length);
            for (int i = 0; i < count; i++)
            {
                string featName = charTokens[i].Trim();
                string rawVal = valTokens[i].Trim();

                if (string.IsNullOrWhiteSpace(featName)) continue;

                if (SmartNumberParser.TryParse(rawVal, out double value))
                {
                    if (!results.ContainsKey(featName))
                    {
                        results.Add(featName, value);
                    }
                }
            }
        }

        private void ExtractRowByRowResults(string[] lines, List<TemplateLineRule> rowRules, TemplateDefinition template, Dictionary<string, double> results)
        {
            var genericRule = rowRules.FirstOrDefault(r => r.LineIndex == 0) ?? rowRules.FirstOrDefault();

            if (genericRule != null)
            {
                // Aplica para todas as linhas a partir de DataStartLine
                int start = Math.Max(1, template.DataStartLine) - 1;

                // 1. Suporte a extração por REGEX com grupos nomeados "feat" e "val"
                if (!string.IsNullOrWhiteSpace(genericRule.RegexPattern))
                {
                    Regex regex;
                    try
                    {
                        regex = new Regex(genericRule.RegexPattern, RegexOptions.Compiled);
                    }
                    catch
                    {
                        regex = new Regex(genericRule.RegexPattern);
                    }

                    for (int i = start; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var match = regex.Match(line);
                        if (match.Success)
                        {
                            string featName = match.Groups["feat"].Success ? match.Groups["feat"].Value.Trim() : string.Empty;
                            string rawVal = match.Groups["val"].Success ? match.Groups["val"].Value.Trim() : string.Empty;

                            if (!string.IsNullOrWhiteSpace(featName) && SmartNumberParser.TryParse(rawVal, out double val))
                            {
                                if (!results.ContainsKey(featName))
                                {
                                    results.Add(featName, val);
                                }
                            }
                        }
                    }
                    return;
                }

                // 2. Extração por Colunas Delimitadas
                string sep = !string.IsNullOrEmpty(genericRule.Separator) ? genericRule.Separator : template.DefaultSeparator;

                for (int i = start; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var tokens = SplitTokens(line, sep);
                    if (genericRule.FeatureColumnIndex >= 0 && genericRule.FeatureColumnIndex < tokens.Length &&
                        genericRule.ValueColumnIndex >= 0 && genericRule.ValueColumnIndex < tokens.Length)
                    {
                        string featName = tokens[genericRule.FeatureColumnIndex].Trim().Trim('"', '\'');
                        string rawVal = tokens[genericRule.ValueColumnIndex].Trim().Trim('"', '\'');

                        if (!string.IsNullOrWhiteSpace(featName) && SmartNumberParser.TryParse(rawVal, out double val))
                        {
                            if (!results.ContainsKey(featName))
                            {
                                results.Add(featName, val);
                            }
                        }
                    }
                }
            }
            else
            {
                // Regras para linhas individuais
                foreach (var rule in rowRules)
                {
                    if (rule.LineIndex <= 0 || rule.LineIndex > lines.Length) continue;

                    var line = lines[rule.LineIndex - 1];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (!string.IsNullOrWhiteSpace(rule.RegexPattern))
                    {
                        var match = Regex.Match(line, rule.RegexPattern);
                        if (match.Success)
                        {
                            string featName = match.Groups["feat"].Success ? match.Groups["feat"].Value.Trim() : string.Empty;
                            string rawVal = match.Groups["val"].Success ? match.Groups["val"].Value.Trim() : string.Empty;

                            if (!string.IsNullOrWhiteSpace(featName) && SmartNumberParser.TryParse(rawVal, out double val))
                            {
                                if (!results.ContainsKey(featName))
                                {
                                    results.Add(featName, val);
                                }
                            }
                        }
                        continue;
                    }

                    string sep = !string.IsNullOrEmpty(rule.Separator) ? rule.Separator : template.DefaultSeparator;
                    var tokens = SplitTokens(line, sep);

                    if (rule.FeatureColumnIndex >= 0 && rule.FeatureColumnIndex < tokens.Length &&
                        rule.ValueColumnIndex >= 0 && rule.ValueColumnIndex < tokens.Length)
                    {
                        string featName = tokens[rule.FeatureColumnIndex].Trim().Trim('"', '\'');
                        string rawVal = tokens[rule.ValueColumnIndex].Trim().Trim('"', '\'');

                        if (!string.IsNullOrWhiteSpace(featName) && SmartNumberParser.TryParse(rawVal, out double val))
                        {
                            if (!results.ContainsKey(featName))
                            {
                                results.Add(featName, val);
                            }
                        }
                    }
                }
            }
        }

        private string ExtractStringFromLine(string line, TemplateLineRule rule)
        {
            if (string.IsNullOrWhiteSpace(line)) return string.Empty;

            // Se houver RegexPattern configurado com grupo de captura
            if (!string.IsNullOrWhiteSpace(rule.RegexPattern))
            {
                try
                {
                    var match = Regex.Match(line, rule.RegexPattern);
                    if (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(rule.RegexGroupName) && match.Groups[rule.RegexGroupName].Success)
                        {
                            return match.Groups[rule.RegexGroupName].Value.Trim();
                        }
                        if (match.Groups["part"].Success) return match.Groups["part"].Value.Trim();
                        if (match.Groups["batch"].Success) return match.Groups["batch"].Value.Trim();
                        if (match.Groups.Count > 1) return match.Groups[1].Value.Trim();
                        return match.Value.Trim();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TemplateExecutionEngine] Erro no Regex: {ex.Message}");
                }
            }

            // Se houver separador e índice de coluna especificado (FeatureColumnIndex)
            if (!string.IsNullOrEmpty(rule.Separator) && line.Contains(rule.Separator))
            {
                var tokens = SplitTokens(line, rule.Separator);
                if (rule.FeatureColumnIndex >= 0 && rule.FeatureColumnIndex < tokens.Length)
                {
                    return tokens[rule.FeatureColumnIndex].Trim().Trim('"', '\'');
                }
            }

            return line.Trim();
        }

        private static string[] SplitTokens(string line, string separator)
        {
            if (string.IsNullOrEmpty(line)) return Array.Empty<string>();
            return line.Split(new[] { separator }, StringSplitOptions.None);
        }
    }

    /// <summary>
    /// Analisador numérico inteligente capaz de interpretar valores decimais tanto com ponto quanto com vírgula,
    /// tratando notações industriais e caracteres não-imprimíveis.
    /// </summary>
    public static class SmartNumberParser
    {
        public static bool TryParse(string raw, out double result)
        {
            result = 0.0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            // Limpeza completa de aspas, apóstrofos e espaços internos/externos
            string cleaned = raw.Trim().Trim('"', '\'', ' ', '\t', '\r', '\n').Trim();

            // 1. Se contém vírgula e não contém ponto (ex: "12,450", "10,5984", "-0,0336"):
            // Tratar explicitamente como decimal substituindo vírgula por ponto
            if (cleaned.Contains(',') && !cleaned.Contains('.'))
            {
                var normalized = cleaned.Replace(',', '.');
                if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                {
                    return true;
                }
            }

            // 2. Se contém ponto e não contém vírgula (ex: "12.450", "10.5984", "-0.0336"):
            if (cleaned.Contains('.') && !cleaned.Contains(','))
            {
                if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                {
                    return true;
                }
            }

            // 3. Tentar leitura direta com CultureInfo.InvariantCulture (apenas Float, sem milhares)
            if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            // 4. Tentar leitura com cultura brasileira (apenas Float, sem milhares)
            var ptBr = CultureInfo.GetCultureInfo("pt-BR");
            if (double.TryParse(cleaned, NumberStyles.Float, ptBr, out result))
            {
                return true;
            }

            return false;
        }
    }
}
