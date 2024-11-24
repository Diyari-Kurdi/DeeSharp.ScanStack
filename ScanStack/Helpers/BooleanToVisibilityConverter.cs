using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ScanStack.Helpers;

public class BooleanToVisibilityConverter : IValueConverter
{
    public BooleanToVisibilityConverter()
    {
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool result)
        {
            if (parameter is string p)
            {
                if (p == "False")
                {
                    return result ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        throw new ArgumentException("BooleanToVisibilityConverterParameterMustBeAnEnumName");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
