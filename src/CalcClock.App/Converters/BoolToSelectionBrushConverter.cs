using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace CalcClock_App.Converters;

/// <summary>Converts a bool into a highlight brush when true, or transparent when false - used to
/// mark the currently selected unit segment in the display.</summary>
public sealed class BoolToSelectionBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isSelected = value is bool b && b;
        string key = isSelected ? "AccentFillColorSecondaryBrush" : "SubtleFillColorTransparentBrush";
        return (Brush)Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
