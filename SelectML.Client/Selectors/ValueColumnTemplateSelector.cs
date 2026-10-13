using System.Windows;
using System.Windows.Controls;

namespace SelectML.Client.Selectors
{
    public class ValueColumnTemplateSelector : DataTemplateSelector
    {
        public DataTemplate VariableTemplate { get; set; } = null!;
        public DataTemplate AttributeTemplate { get; set; } = null!;

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ResultItem result && result.IsAttribute)
            {
                return AttributeTemplate;
            }
            return VariableTemplate;
        }
    }
}
