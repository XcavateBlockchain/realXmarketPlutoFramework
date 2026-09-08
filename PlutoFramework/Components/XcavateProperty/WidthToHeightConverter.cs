using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlutoFramework.Components.XcavateProperty
{
    internal class WidthToHeightConverter : IValueConverter
    {
        // The SDK's nullable analysis still flags the boxed-unbox (CS8605) even after value!.
        #pragma warning disable CS8605
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (double)value! * 2.0 / 3.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (double)value! * 3.0 / 2.0;
        }
        #pragma warning restore CS8605
    }
}
