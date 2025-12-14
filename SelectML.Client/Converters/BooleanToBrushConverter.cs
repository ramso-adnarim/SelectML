using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SelectML.Client.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLocked && isLocked)
            {
                // Se está travado (Monitorando), botão fica Laranja/Amarelo (Ação de Editar/Pausar)
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0)); // DarkOrange
            }

            // Se não está travado (Editando), botão fica Azul (Ação de Salvar/Iniciar)
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204)); // #007ACC
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}