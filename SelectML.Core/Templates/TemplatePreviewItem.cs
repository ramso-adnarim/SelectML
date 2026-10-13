using System.Globalization;

namespace SelectML.Core.Templates
{
    /// <summary>
    /// Representa um item individual na prévia interativa do resultado da medição.
    /// </summary>
    public class TemplatePreviewItem
    {
        public string FeatureName { get; set; } = string.Empty;
        public double Value { get; set; }
        public string FormattedValue => Value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
