using System.Windows;
using System.Windows.Controls;

namespace SelectML.Client.Selectors
{
    public class FeatureColumnTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ReadOnlyTemplate { get; set; }
        public DataTemplate EditableTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ResultItem result)
            {
                if (result.IsEditable)
                {
                    return EditableTemplate;
                }
            }
            return ReadOnlyTemplate;
        }
    }
}
