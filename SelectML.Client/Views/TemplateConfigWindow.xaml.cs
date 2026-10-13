using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SelectML.Client.ViewModels;
using SelectML.Core;
using SelectML.Core.Templates;

// Desambiguação de tipos entre WPF e Windows Forms
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using Application = System.Windows.Application;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace SelectML.Client.Views
{
    public partial class TemplateConfigWindow : Window
    {
        private sealed class LineInfo
        {
            public int LineNumber { get; set; }
            public string RawText { get; set; } = string.Empty;
        }

        // Pincéis estáticos pré-compilados para as marcações de cores do template
        private static readonly Brush BrushPartNameBg = new SolidColorBrush(Color.FromArgb(0x40, 0xE0, 0x6C, 0x75)); // Vermelho suave
        private static readonly Brush BrushPartNameFg = new SolidColorBrush(Color.FromRgb(0xFF, 0x85, 0x8D));

        private static readonly Brush BrushBatchNumberBg = new SolidColorBrush(Color.FromArgb(0x40, 0xC6, 0x78, 0xDD)); // Roxo suave
        private static readonly Brush BrushBatchNumberFg = new SolidColorBrush(Color.FromRgb(0xDB, 0x9B, 0xEE));

        private static readonly Brush BrushCharacteristicsBg = new SolidColorBrush(Color.FromArgb(0x40, 0x61, 0xAF, 0xEF)); // Azul suave
        private static readonly Brush BrushCharacteristicsFg = new SolidColorBrush(Color.FromRgb(0x7A, 0xBD, 0xF2));

        private static readonly Brush BrushValuesBg = new SolidColorBrush(Color.FromArgb(0x40, 0x98, 0xC3, 0x79)); // Verde suave
        private static readonly Brush BrushValuesFg = new SolidColorBrush(Color.FromRgb(0xB0, 0xD4, 0x95));

        private static readonly Brush BrushRowFeatureBg = new SolidColorBrush(Color.FromArgb(0x40, 0xE5, 0xC0, 0x7B)); // Âmbar suave
        private static readonly Brush BrushRowFeatureFg = new SolidColorBrush(Color.FromRgb(0xEC, 0xCE, 0x96));

        static TemplateConfigWindow()
        {
            BrushPartNameBg.Freeze();
            BrushPartNameFg.Freeze();
            BrushBatchNumberBg.Freeze();
            BrushBatchNumberFg.Freeze();
            BrushCharacteristicsBg.Freeze();
            BrushCharacteristicsFg.Freeze();
            BrushValuesBg.Freeze();
            BrushValuesFg.Freeze();
            BrushRowFeatureBg.Freeze();
            BrushRowFeatureFg.Freeze();
        }

        public TemplateConfigViewModel ViewModel { get; }
        public RichTextBox Editor => SampleTextEditor;

        public TemplateConfigWindow(IConfigurableParser? parser = null)
        {
            InitializeComponent();
            ViewModel = new TemplateConfigViewModel(parser);
            DataContext = ViewModel;

            ViewModel.RequestClose += (result) =>
            {
                try
                {
                    DialogResult = result;
                }
                catch (InvalidOperationException)
                {
                    // Janela não exibida como caixa de diálogo modal (ShowDialog)
                }
                Close();
            };

            ViewModel.SampleFileLoaded += (content) => LoadDocument(content);
            ViewModel.RulesChanged += () => ApplyDocumentHighlighting();
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.SelectedTemplate))
                {
                    ApplyDocumentHighlighting();
                }
            };

            Loaded += (s, e) =>
            {
                SampleTextEditor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnEditorScrollChanged));

                if (!string.IsNullOrWhiteSpace(ViewModel.SampleFileContent))
                {
                    LoadDocument(ViewModel.SampleFileContent);
                }
            };
        }

        private void OnEditorScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0 && LineNumbersScroll != null)
            {
                LineNumbersScroll.ScrollToVerticalOffset(e.VerticalOffset);
            }
        }

        public void LoadDocument(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                SampleTextEditor.Document = new FlowDocument();
                TxtLineNumbers.Text = string.Empty;
                return;
            }

            var doc = new FlowDocument
            {
                PageWidth = 5000, // Previne quebras de linha automáticas em arquivos CSV largos
                PagePadding = new Thickness(0)
            };

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var lineNumbersBuilder = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNum = i + 1;
                lineNumbersBuilder.AppendLine(lineNum.ToString());

                var info = new LineInfo { LineNumber = lineNum, RawText = lines[i] };
                var para = new Paragraph
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(2, 0, 2, 0),
                    LineHeight = 20,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    Tag = info
                };

                var run = new Run(lines[i]);
                para.Inlines.Add(run);
                doc.Blocks.Add(para);
            }

            TxtLineNumbers.Text = lineNumbersBuilder.ToString().TrimEnd();
            SampleTextEditor.Document = doc;

            ApplyDocumentHighlighting();
        }

        public void ApplyDocumentHighlighting()
        {
            if (SampleTextEditor.Document == null || ViewModel.SelectedTemplate == null)
                return;

            var rules = ViewModel.SelectedTemplate.LineRules;
            int dataStartLine = ViewModel.SelectedTemplate.DataStartLine;
            var rowRule = rules.FirstOrDefault(r => r.Role == LineRuleRole.RowFeatureAndValue);

            Brush defaultFg = Application.Current.TryFindResource("Brush.Text.Primary") as Brush ?? Brushes.White;

            var blocks = SampleTextEditor.Document.Blocks.ToList();
            foreach (var block in blocks)
            {
                if (block is Paragraph para && para.Tag is LineInfo info)
                {
                    int lineIndex = info.LineNumber;
                    string rawText = info.RawText;

                    var rule = rules.FirstOrDefault(r => r.LineIndex == lineIndex);

                    if (rule != null && rule.Role != LineRuleRole.RowFeatureAndValue)
                    {
                        para.Inlines.Clear();
                        para.Inlines.Add(new Run(rawText));

                        switch (rule.Role)
                        {
                            case LineRuleRole.PartName:
                                para.Background = BrushPartNameBg;
                                para.Foreground = BrushPartNameFg;
                                break;
                            case LineRuleRole.BatchNumber:
                                para.Background = BrushBatchNumberBg;
                                para.Foreground = BrushBatchNumberFg;
                                break;
                            case LineRuleRole.CharacteristicNames:
                                para.Background = BrushCharacteristicsBg;
                                para.Foreground = BrushCharacteristicsFg;
                                break;
                            case LineRuleRole.MeasuredValues:
                                para.Background = BrushValuesBg;
                                para.Foreground = BrushValuesFg;
                                break;
                        }
                    }
                    else if (rowRule != null && lineIndex >= dataStartLine)
                    {
                        HighlightRowByRowLine(para, info, rowRule, defaultFg);
                    }
                    else
                    {
                        para.Inlines.Clear();
                        para.Inlines.Add(new Run(rawText));
                        para.Background = Brushes.Transparent;
                        para.Foreground = defaultFg;
                    }
                }
            }
        }

        private void HighlightRowByRowLine(Paragraph para, LineInfo info, TemplateLineRule rowRule, Brush defaultFg)
        {
            string line = info.RawText;

            // 1. Caso delimitado: coloração independente de tokens por coluna
            if (!string.IsNullOrEmpty(rowRule.Separator) && line.Contains(rowRule.Separator))
            {
                para.Background = Brushes.Transparent;
                para.Foreground = defaultFg;
                para.Inlines.Clear();

                string sep = rowRule.Separator;
                var tokens = line.Split(new[] { sep }, StringSplitOptions.None);

                for (int c = 0; c < tokens.Length; c++)
                {
                    if (c > 0)
                    {
                        para.Inlines.Add(new Run(sep) { Foreground = defaultFg });
                    }

                    var tokenRun = new Run(tokens[c]);
                    if (c == rowRule.FeatureColumnIndex)
                    {
                        tokenRun.Background = BrushCharacteristicsBg;
                        tokenRun.Foreground = BrushCharacteristicsFg;
                        tokenRun.FontWeight = FontWeights.SemiBold;
                    }
                    else if (c == rowRule.ValueColumnIndex)
                    {
                        tokenRun.Background = BrushValuesBg;
                        tokenRun.Foreground = BrushValuesFg;
                        tokenRun.FontWeight = FontWeights.SemiBold;
                    }
                    else
                    {
                        tokenRun.Foreground = defaultFg;
                    }

                    para.Inlines.Add(tokenRun);
                }
                return;
            }

            // 2. Caso REGEX: coloração de grupos de captura nomeados "feat" e "val"
            if (!string.IsNullOrWhiteSpace(rowRule.RegexPattern))
            {
                try
                {
                    var match = Regex.Match(line, rowRule.RegexPattern);
                    if (match.Success)
                    {
                        para.Background = Brushes.Transparent;
                        para.Foreground = defaultFg;
                        para.Inlines.Clear();

                        var groups = new[]
                        {
                            new { Name = "feat", Group = match.Groups["feat"], Bg = BrushCharacteristicsBg, Fg = BrushCharacteristicsFg },
                            new { Name = "val", Group = match.Groups["val"], Bg = BrushValuesBg, Fg = BrushValuesFg }
                        }.Where(g => g.Group.Success).OrderBy(g => g.Group.Index).ToList();

                        int currIndex = 0;
                        foreach (var g in groups)
                        {
                            if (g.Group.Index > currIndex)
                            {
                                para.Inlines.Add(new Run(line.Substring(currIndex, g.Group.Index - currIndex)) { Foreground = defaultFg });
                            }

                            para.Inlines.Add(new Run(g.Group.Value)
                            {
                                Background = g.Bg,
                                Foreground = g.Fg,
                                FontWeight = FontWeights.SemiBold
                            });

                            currIndex = g.Group.Index + g.Group.Length;
                        }

                        if (currIndex < line.Length)
                        {
                            para.Inlines.Add(new Run(line.Substring(currIndex)) { Foreground = defaultFg });
                        }
                        return;
                    }
                }
                catch
                {
                    // Fallback para marcação inteira abaixo em caso de falha de regex
                }
            }

            // 3. Fallback: marcação em bloco da linha inteira
            para.Inlines.Clear();
            para.Inlines.Add(new Run(line));
            para.Background = BrushRowFeatureBg;
            para.Foreground = BrushRowFeatureFg;
        }

        private (int lineIndex, string fullLine, string selectedText, int selectionStart, int selectionLength) GetSelectionContext()
        {
            var selection = SampleTextEditor.Selection;
            Paragraph? para = null;

            if (selection != null && !selection.IsEmpty)
            {
                para = selection.Start.Paragraph;
            }

            if (para == null)
            {
                var mousePos = Mouse.GetPosition(SampleTextEditor);
                var pointer = SampleTextEditor.GetPositionFromPoint(mousePos, true);
                para = pointer?.Paragraph;
            }

            if (para == null && SampleTextEditor.CaretPosition != null)
            {
                para = SampleTextEditor.CaretPosition.Paragraph;
            }

            if (para == null || para.Tag is not LineInfo info)
            {
                return (0, string.Empty, string.Empty, 0, 0);
            }

            int lineIndex = info.LineNumber;
            string fullLine = info.RawText;

            string selectedText = selection?.Text?.Trim() ?? string.Empty;
            int selectionStart = 0;
            int selectionLength = selectedText.Length;

            if (selection != null && !selection.IsEmpty)
            {
                selectionStart = new TextRange(para.ContentStart, selection.Start).Text.Length;
            }
            else
            {
                var mousePos = Mouse.GetPosition(SampleTextEditor);
                var pointer = SampleTextEditor.GetPositionFromPoint(mousePos, true);
                if (pointer != null)
                {
                    selectionStart = new TextRange(para.ContentStart, pointer).Text.Length;
                }
                else if (SampleTextEditor.CaretPosition != null)
                {
                    selectionStart = new TextRange(para.ContentStart, SampleTextEditor.CaretPosition).Text.Length;
                }
            }

            if (selectionStart < 0) selectionStart = 0;
            if (selectionStart > fullLine.Length) selectionStart = fullLine.Length;

            return (lineIndex, fullLine, selectedText, selectionStart, selectionLength);
        }

        private void OnMarkPartNameClick(object sender, RoutedEventArgs e)
        {
            var (lineIndex, fullLine, selectedText, start, len) = GetSelectionContext();
            if (lineIndex <= 0) return;

            ViewModel.ApplyRule(LineRuleRole.PartName, lineIndex, fullLine, selectedText, start, len);
            ApplyDocumentHighlighting();
        }

        private void OnMarkBatchNumberClick(object sender, RoutedEventArgs e)
        {
            var (lineIndex, fullLine, selectedText, start, len) = GetSelectionContext();
            if (lineIndex <= 0) return;

            ViewModel.ApplyRule(LineRuleRole.BatchNumber, lineIndex, fullLine, selectedText, start, len);
            ApplyDocumentHighlighting();
        }

        private void OnMarkCharacteristicNamesClick(object sender, RoutedEventArgs e)
        {
            var (lineIndex, fullLine, selectedText, start, len) = GetSelectionContext();
            if (lineIndex <= 0) return;

            ViewModel.ApplyRule(LineRuleRole.CharacteristicNames, lineIndex, fullLine, selectedText, start, len);
            ApplyDocumentHighlighting();
        }

        private void OnMarkMeasuredValuesClick(object sender, RoutedEventArgs e)
        {
            var (lineIndex, fullLine, selectedText, start, len) = GetSelectionContext();
            if (lineIndex <= 0) return;

            ViewModel.ApplyRule(LineRuleRole.MeasuredValues, lineIndex, fullLine, selectedText, start, len);
            ApplyDocumentHighlighting();
        }

        private void OnMarkRowFeatureColumnClick(object sender, RoutedEventArgs e)
        {
            var (lineIndex, fullLine, selectedText, start, len) = GetSelectionContext();
            if (lineIndex <= 0) return;

            ViewModel.ApplyRule(LineRuleRole.RowFeatureColumn, lineIndex, fullLine, selectedText, start, len);
            ApplyDocumentHighlighting();
        }

        private void OnMarkRowValueColumnClick(object sender, RoutedEventArgs e)
        {
            var (lineIndex, fullLine, selectedText, start, len) = GetSelectionContext();
            if (lineIndex <= 0) return;

            ViewModel.ApplyRule(LineRuleRole.RowValueColumn, lineIndex, fullLine, selectedText, start, len);
            ApplyDocumentHighlighting();
        }

        private void OnMarkRowFeatureAndValueClick(object sender, RoutedEventArgs e)
        {
            var (lineIndex, fullLine, selectedText, start, len) = GetSelectionContext();
            if (lineIndex <= 0) return;

            ViewModel.ApplyRule(LineRuleRole.RowFeatureAndValue, lineIndex, fullLine, selectedText, start, len);
            ApplyDocumentHighlighting();
        }

        private void OnRemoveLineMarkingClick(object sender, RoutedEventArgs e)
        {
            var (lineIndex, _, _, _, _) = GetSelectionContext();
            if (lineIndex <= 0) return;

            ViewModel.RemoveRuleForLine(lineIndex);
            ApplyDocumentHighlighting();
        }

        private void OnSampleFileDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void OnSampleFileDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    ViewModel.LoadSampleFile(files[0]);
                }
            }
        }
    }
}
