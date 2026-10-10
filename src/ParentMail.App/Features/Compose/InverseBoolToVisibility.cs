using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ParentMail.App.Features.Compose;

/// <summary>true のとき隠し、false のとき表示する（「BCCを追加」ボタンの出し入れ用）。</summary>
public sealed class InverseBoolToVisibility : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
