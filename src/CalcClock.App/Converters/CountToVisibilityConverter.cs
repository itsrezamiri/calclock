using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace CalcClock_App.Converters;

/// <summary>Visible when the bound count is zero; Collapsed otherwise. Pass ConverterParameter="Invert" to flip.</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isZero = value is int count && count == 0;
        bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        if (invert)
        {
            isZero = !isZero;
        }

        return isZero ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
