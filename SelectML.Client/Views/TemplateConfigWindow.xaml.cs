using System;
using System.IO;
using System.Linq;
using System.Text;
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

namespace SelectML.Client.Views
{
    public partial class TemplateConfigWindow : Window
    {
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

                var para = new Paragraph
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(2, 0, 2, 0),
                    LineHeight = 20,
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    Tag = lineNum
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
            bool hasRowByRow = rules.Any(r => r.Role == LineRuleRole.RowFeatureAndValue);

            Brush defaultFg = Application.Current.TryFindResource("Brush.Text.Primary") as Brush ?? Brushes.White;

            foreach (var block in SampleTextEditor.Document.Blocks)
            {
                if (block is Paragraph para && para.Tag is int lineIndex)
                {
                    Brush bg = Brushes.Transparent;
                    Brush fg = defaultFg;

                    var rule = rules.FirstOrDefault(r => r.LineIndex == lineIndex);
                    if (rule != null)
                    {
                        switch (rule.Role)
                        {
                            case LineRuleRole.PartName:
                                bg = BrushPartNameBg;
                                fg = BrushPartNameFg;
                                break;
                            case LineRuleRole.BatchNumber:
                                bg = BrushBatchNumberBg;
                                fg = BrushBatchNumberFg;
                                break;
                            case LineRuleRole.CharacteristicNames:
                                bg = BrushCharacteristicsBg;
                                fg = BrushCharacteristicsFg;
                                break;
                            case LineRuleRole.MeasuredValues:
                                bg = BrushValuesBg;
                                fg = BrushValuesFg;
                                break;
                            case LineRuleRole.RowFeatureAndValue:
                                bg = BrushRowFeatureBg;
                                fg = BrushRowFeatureFg;
                                break;
                        }
                    }
                    else if (hasRowByRow && lineIndex >= dataStartLine)
                    {
                        bg = BrushRowFeatureBg;
                        fg = BrushRowFeatureFg;
                    }

                    para.Background = bg;
                    para.Foreground = fg;
                }
            }
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

            if (para == null || para.Tag is not int lineIndex)
            {
                return (0, string.Empty, string.Empty, 0, 0);
            }

            var fullLineRange = new TextRange(para.ContentStart, para.ContentEnd);
            string fullLine = fullLineRange.Text.TrimEnd('\r', '\n');

            string selectedText = selection?.Text?.Trim() ?? string.Empty;
            int selectionStart = 0;
            int selectionLength = selectedText.Length;

            if (!string.IsNullOrEmpty(selectedText))
            {
                selectionStart = fullLine.IndexOf(selectedText, StringComparison.Ordinal);
                if (selectionStart < 0) selectionStart = 0;
            }

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
