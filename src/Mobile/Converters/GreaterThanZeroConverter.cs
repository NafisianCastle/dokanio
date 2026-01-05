using System.Globalization;

namespace Mobile.Converters;

public class GreaterThanZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
            return decimalValue > 0;
        
        if (value is double doubleValue)
            return doubleValue > 0;
        
        if (value is float floatValue)
            return floatValue > 0;
        
        if (value is int intValue)
            return intValue > 0;
        
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}