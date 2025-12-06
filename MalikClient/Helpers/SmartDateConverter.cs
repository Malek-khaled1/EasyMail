using System;
using System.Globalization;
using System.Windows.Data;

namespace MalikClient.Helpers
{
    public class SmartDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTimeOffset date)
            {
                var now = DateTimeOffset.Now;

                // 1. Hvis det er i dag -> Vis kun klokkeslæt
                if (date.Date == now.Date)
                {
                    return date.ToString("HH:mm");
                }

                // 2. Hvis det er i år -> Vis dato og måned
                if (date.Year == now.Year)
                {
                    return date.ToString("dd MMM");
                }

                // 3. Ellers (gammelt år) -> Vis dato, måned og år
                return date.ToString("dd MMM yyyy");
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}