using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.UI.Xaml.Data;

namespace ScanStack.Helpers;
public class EnumToDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is Enum enumValue &&
            enumValue.GetType()
                .GetMember(enumValue.ToString())
                .FirstOrDefault()
                    ?.GetCustomAttribute<DisplayAttribute>()
                    ?.GetName() is string displayName
                        ? displayName
                        : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
