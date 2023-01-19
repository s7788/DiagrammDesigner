using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace DiagramDesigner.Converter
{
    /// <summary>
    ///計算Arc 連接點
    ///以右上角為中心 ,左邊點 Widht/3 , 右邊點 Height /3
    /// </summary>
    public class ObjectMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Thickness result = new Thickness(0);

            if (value == null || parameter == null)
                return result;

            if (!(value.GetType() == (typeof(Double))))
                return result;

            switch (parameter.ToString().Trim().ToUpper())
            {
                case "WIDTH":
                    double width = (double)value / 3;
                    result = new Thickness(width, 0, width, 0);
                    break;
                case "HEIGHT":
                    double height = (double)value / 3;
                    result = new Thickness(0, height, 0, height);
                    break;
                default:
                    result = new Thickness(0);
                    break;
            }
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
