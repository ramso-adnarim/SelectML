using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text.RegularExpressions;
using SelectML.Client.ViewModels;

namespace SelectML.Client.Views
{
    public partial class NameModifierConfigWindow : Window
    {
        public NameModifierConfigWindow()
        {
            InitializeComponent();
            
            var vm = new NameModifierConfigViewModel();
            this.DataContext = vm;

            vm.RequestClose += () => this.Close();
            vm.RequestInsertToken += (token) =>
            {
                int caretIndex = FormatTextBox.CaretIndex;
                FormatTextBox.Text = FormatTextBox.Text.Insert(caretIndex, token);
                FormatTextBox.CaretIndex = caretIndex + token.Length;
                FormatTextBox.Focus();
                vm.CustomNameModifierFormat = FormatTextBox.Text;
            };
        }

        private void FormatTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SyntaxHighlightTextBlock.Inlines.Clear();
            string text = FormatTextBox.Text;
            if (string.IsNullOrEmpty(text)) return;

            // Simple parser: Split text by regex that matches our tags {N,d,M} and {T,d,M}
            string pattern = @"(\{N,\d,[AT]\}|\{T,\d,[AT]\})";
            var parts = Regex.Split(text, pattern);
            
            System.Windows.Media.Brush tagBrush = (System.Windows.Media.Brush)FindResource("Brush.Action.Primary");
            System.Windows.Media.Brush errorBrush = (System.Windows.Media.Brush)FindResource("Brush.Status.Error");
            System.Windows.Media.Brush defaultBrush = (System.Windows.Media.Brush)FindResource("Brush.Text.Primary");

            foreach (var part in parts)
            {
                if (Regex.IsMatch(part, "^" + pattern + "$"))
                {
                    SyntaxHighlightTextBlock.Inlines.Add(new Run(part) { Foreground = tagBrush, FontWeight = FontWeights.Bold });
                }
                else
                {
                    // Check if there's any stray { or } indicating a broken tag
                    if (part.Contains("{") || part.Contains("}"))
                    {
                        SyntaxHighlightTextBlock.Inlines.Add(new Run(part) { Foreground = errorBrush });
                    }
                    else
                    {
                        SyntaxHighlightTextBlock.Inlines.Add(new Run(part) { Foreground = defaultBrush });
                    }
                }
            }
        }
    }
}
