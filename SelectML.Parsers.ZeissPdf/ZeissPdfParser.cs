using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SelectML.Core;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SelectML.Parsers.ZeissPdf
{
    public class ZeissPdfParser : IMachineParser
    {
        public string MachineName => "Zeiss Calypso PDF";

        public bool CanParse(string filePath)
        {
            return filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public MeasurementData Parse(string filePath)
        {
            var data = new MeasurementData
            {
                Results = new Dictionary<string, double>()
            };

            try
            {
                var config = ZeissPdfConfig.Load();
                var context = new ParseContext();

                using (var document = PdfDocument.Open(filePath))
                {
                    if (document.NumberOfPages == 0) return data;

                    var firstPage = document.GetPage(1);
                    
                    // Extração de cabeçalhos baseada em posicionamento espacial (Evita bugs de fluxo de texto interno do PDF)
                    var firstPageWords = firstPage.GetWords().OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();
                    var headerLines = GroupWordsIntoLines(firstPageWords);

                    data.PartName = ExtractHeaderSpatial(headerLines, config.PartNameLabel);
                    data.BatchNumber = ExtractHeaderSpatial(headerLines, config.BatchNumberLabel);

                    string timeDateStr = ExtractHeaderSpatial(headerLines, "Time/Date");
                    if (!string.IsNullOrEmpty(timeDateStr))
                    {
                        if (DateTime.TryParseExact(timeDateStr, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                        {
                            data.MeasureDate = parsedDate;
                        }
                        else if (DateTime.TryParse(timeDateStr, new CultureInfo("pt-BR"), DateTimeStyles.None, out DateTime fallbackDate))
                        {
                            data.MeasureDate = fallbackDate;
                        }
                    }

                    // Executa a varredura e injeta os resultados numéricos
                    foreach (var page in document.GetPages())
                    {
                        ParseTablePage(page, data, context);
                    }

                    // Pós-processamento: Aplica o limite de casas decimais aos ângulos (Máx 6)
                    int finalPrecision = context.MaxDecimals == 0 ? 3 : Math.Min(context.MaxDecimals, 6);
                    foreach (var angle in context.PendingAngles)
                    {
                        data.Results[angle.Key] = Math.Round(angle.Value, finalPrecision);
                    }
                }
            }
            catch (Exception)
            {
                // Tratamento de falha silencioso
            }

            return data;
        }

        private string ExtractHeaderSpatial(List<List<Word>> lines, string label)
        {
            string[] labelWords = label.Split(' ');
            string[] knownLabels = { "Drawing number", "Order number", "Variant", "Company", "Department", "Modelo MMC", "Modelo", "Nº MMC", "Nº", "Corrida", "Operator", "Text", "Time/Date", "Run", "Part ident", "Last", "Number", "►", "Approval", "Duração" };

            foreach (var line in lines)
            {
                var lineWords = line.OrderBy(w => w.BoundingBox.Left).ToList();

                for (int i = 0; i <= lineWords.Count - labelWords.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < labelWords.Length; j++)
                    {
                        if (!lineWords[i + j].Text.Equals(labelWords[j], StringComparison.OrdinalIgnoreCase))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        int valueStartIndex = i + labelWords.Length;
                        var valueWords = new List<Word>();

                        for (int k = valueStartIndex; k < lineWords.Count; k++)
                        {
                            // Relaxamos o gap inicial para 150pt (Tabulações longas da coluna direita como em "Run           Todas")
                            if (k == valueStartIndex && k > 0)
                            {
                                double initialGap = lineWords[k].BoundingBox.Left - lineWords[k - 1].BoundingBox.Right;
                                if (initialGap > 150.0) break;
                            }

                            if (k > valueStartIndex)
                            {
                                double gap = lineWords[k].BoundingBox.Left - lineWords[k - 1].BoundingBox.Right;
                                if (gap > 100.0) break;
                            }

                            bool hitLabel = false;
                            string textFromK = string.Join(" ", lineWords.Skip(k).Select(w => w.Text));
                            foreach (var l in knownLabels)
                            {
                                if (textFromK.StartsWith(l, StringComparison.OrdinalIgnoreCase))
                                {
                                    hitLabel = true;
                                    break;
                                }
                            }
                            if (hitLabel) break;

                            valueWords.Add(lineWords[k]);
                        }

                        string val = string.Join(" ", valueWords.Select(w => w.Text)).Trim();

                        if (string.IsNullOrEmpty(val))
                        {
                            int lineIndex = lines.IndexOf(line);
                            if (lineIndex + 1 < lines.Count)
                            {
                                var nextLineWords = lines[lineIndex + 1].OrderBy(w => w.BoundingBox.Left).ToList();
                                double labelLeft = lineWords[i].BoundingBox.Left;
                                var alignedWords = new List<Word>();

                                for (int k = 0; k < nextLineWords.Count; k++)
                                {
                                    // IGNORA palavras que estejam para trás do rótulo! (Ex: Rótulo "Run" na direita, ignora "Nº MMC" na esquerda)
                                    if (nextLineWords[k].BoundingBox.Left < labelLeft - 20.0) continue;

                                    if (alignedWords.Count == 0 && nextLineWords[k].BoundingBox.Left > labelLeft + 200.0) break;

                                    if (alignedWords.Count > 0)
                                    {
                                        double gap = nextLineWords[k].BoundingBox.Left - alignedWords.Last().BoundingBox.Right;
                                        if (gap > 100.0) break;
                                    }

                                    bool hitLabel = false;
                                    string textFromK = string.Join(" ", nextLineWords.Skip(k).Select(w => w.Text));
                                    foreach (var l in knownLabels)
                                    {
                                        if (textFromK.StartsWith(l, StringComparison.OrdinalIgnoreCase))
                                        {
                                            hitLabel = true;
                                            break;
                                        }
                                    }
                                    if (hitLabel) break;

                                    alignedWords.Add(nextLineWords[k]);
                                }

                                val = string.Join(" ", alignedWords.Select(w => w.Text)).Trim();
                            }
                        }

                        return val;
                    }
                }
            }

            return string.Empty;
        }

        private class ParseContext
        {
            public int MaxDecimals { get; set; } = 0;
            public List<KeyValuePair<string, double>> PendingAngles { get; set; } = new List<KeyValuePair<string, double>>();
        }

        private void ParseTablePage(Page page, MeasurementData data, ParseContext context)
        {
            var words = page.GetWords()
                .OrderByDescending(w => w.BoundingBox.Bottom)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            var lines = GroupWordsIntoLines(words);
            bool inTable = false;

            foreach (var lineWords in lines)
            {
                string lineText = string.Join(" ", lineWords.Select(w => w.Text));

                // Start
                if (!inTable && (lineText.Contains("Measured value Nominal value") || lineText.Contains("Name Measured value")))
                {
                    inTable = true;
                    continue;
                }

                // Stop
                if (inTable && (lineText.StartsWith("Text Event") || lineText.StartsWith("n.def.") || lineText.Contains("Page ")))
                {
                    inTable = false;
                    continue;
                }

                if (inTable)
                {
                    ExtractFeatureFromLine(lineWords, data, context);
                }
            }
        }

        private List<List<Word>> GroupWordsIntoLines(List<Word> words)
        {
            var lines = new List<List<Word>>();
            if (words.Count == 0) return lines;

            var currentLine = new List<Word> { words[0] };
            double currentBaseline = words[0].BoundingBox.Bottom;

            for (int i = 1; i < words.Count; i++)
            {
                if (Math.Abs(words[i].BoundingBox.Bottom - currentBaseline) < 3.0)
                {
                    currentLine.Add(words[i]);
                }
                else
                {
                    lines.Add(new List<Word>(currentLine));
                    currentLine.Clear();
                    currentLine.Add(words[i]);
                    currentBaseline = words[i].BoundingBox.Bottom;
                }
            }
            if (currentLine.Count > 0)
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private void ExtractFeatureFromLine(List<Word> lineWords, MeasurementData data, ParseContext context)
        {
            if (lineWords.Count < 2) return;

            lineWords = lineWords.OrderBy(w => w.BoundingBox.Left).ToList();

            var columns = new List<string>();
            var currentCol = new List<string> { lineWords[0].Text };

            for (int i = 1; i < lineWords.Count; i++)
            {
                double gap = lineWords[i].BoundingBox.Left - lineWords[i - 1].BoundingBox.Right;
                if (gap > 8.0) 
                {
                    columns.Add(string.Join(" ", currentCol));
                    currentCol.Clear();
                }
                currentCol.Add(lineWords[i].Text);
            }
            columns.Add(string.Join(" ", currentCol));

            if (columns.Count < 2) return; 

            string featureName = columns[0].Trim();
            string valuesString = string.Join(" ", columns.Skip(1)).Replace("$", "");

            // 1. Tenta extrair GMS
            string gmsPattern = @"(-?\d+)(?:°|\^\{\\circ\}|\\circ)\s*(\d+)(?:'|\^\{\\prime\}|\\prime)\s*(\d+)(?:" + "\"" + @"|''|\^\{\\prime\\prime\}|\\prime\\prime)";
            var gmsMatches = Regex.Matches(valuesString, gmsPattern);
            
            if (gmsMatches.Count >= 1)
            {
                string measuredDegStr = gmsMatches[0].Groups[1].Value;
                string measuredMinStr = gmsMatches[0].Groups[2].Value;
                string measuredSecStr = gmsMatches[0].Groups[3].Value;
                
                // Conversão para Grau Decimal usando InvariantCulture pois são strings inteiras
                if (double.TryParse(measuredDegStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double graus) &&
                    double.TryParse(measuredMinStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double minutos) &&
                    double.TryParse(measuredSecStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double segundos))
                {
                    double sinal = graus < 0 ? -1 : 1;
                    double grauDecimal = (Math.Abs(graus) + (minutos / 60.0) + (segundos / 3600.0)) * sinal;
                    
                    // Adiciona na lista pendente para limitar as casas decimais ao final do Parsing
                    context.PendingAngles.Add(new KeyValuePair<string, double>(featureName, grauDecimal));
                }
                return;
            }

            // 2. Tenta extrair Decimais lineares
            var decMatches = Regex.Matches(valuesString, @"(-?\d+[.,](\d+))");
            if (decMatches.Count >= 1)
            {
                string measuredDecStr = decMatches[0].Groups[1].Value;
                string fractionalPart = decMatches[0].Groups[2].Value;
                
                // Atualiza o rastreamento do número de casas decimais para guiar o arredondamento dos ângulos
                context.MaxDecimals = Math.Max(context.MaxDecimals, fractionalPart.Length);
                
                // Normaliza ponto ou vírgula para vírgula, de modo que a pt-BR não falhe em ambientes mistos
                string normalizedDec = measuredDecStr.Replace(".", ",");
                
                if (double.TryParse(normalizedDec, NumberStyles.Any, new CultureInfo("pt-BR"), out double measuredDec))
                {
                    data.Results[featureName] = measuredDec;
                }
                return;
            }
        }
    }
}
